using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Services;
using MySql.Data.MySqlClient;

namespace ProjectTemplate
{
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [System.Web.Script.Services.ScriptService]
    public class ProjectServices : System.Web.Services.WebService
    {
        ////////////////////////////////////////////////////////////////////////
        /// Replace the values of these variables with your database credentials.
        ////////////////////////////////////////////////////////////////////////
        private string dbID = "cis440sum26team3";
        private string dbPass = "cis440sum26team3";
        private string dbName = "cis440sum26team3";

        private const int DefaultCheckInFrequencyDays = 7;
        private const string AnonymousCheckInCookieName = "cis440AnonymousCheckInToken";

        ////////////////////////////////////////////////////////////////////////
        /// Call this method anywhere that you need the connection string.
        ////////////////////////////////////////////////////////////////////////
        private string getConString()
        {
            return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName
                + "; UID=" + dbID + "; PASSWORD=" + dbPass;
        }

        // Shared manager authorization check used by the US-07 manager services.
        private bool IsManagerSession()
        {
            return Session["IsManager"] != null
                && Session["IsManager"].ToString()
                    .Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // US-07:
        // Creates a random browser-scoped control token if one does not exist.
        // The token contains no employee name, email, employee ID, mood data,
        // or other feedback content.
        private string GetOrCreateAnonymousCheckInToken()
        {
            HttpCookie existingCookie =
                Context.Request.Cookies[AnonymousCheckInCookieName];

            if (existingCookie != null
                && !string.IsNullOrWhiteSpace(existingCookie.Value))
            {
                return existingCookie.Value;
            }

            string newToken = Guid.NewGuid().ToString("N");

            HttpCookie controlCookie =
                new HttpCookie(AnonymousCheckInCookieName, newToken);

            controlCookie.HttpOnly = true;
            controlCookie.Secure = Context.Request.IsSecureConnection;
            controlCookie.Expires = DateTime.UtcNow.AddYears(1);

            Context.Response.Cookies.Set(controlCookie);

            return newToken;
        }

        // US-07:
        // The database receives only a SHA-256 hash of the random control token.
        private string HashAnonymousCheckInToken(string token)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] tokenBytes = Encoding.UTF8.GetBytes(token);
                byte[] hashBytes = sha256.ComputeHash(tokenBytes);

                StringBuilder builder = new StringBuilder(hashBytes.Length * 2);

                for (int i = 0; i < hashBytes.Length; i++)
                {
                    builder.Append(hashBytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        // US-07:
        // Reads the company-wide saved frequency. The transaction argument is
        // used by SubmitMoodCheckIn so the eligibility check and insert occur
        // together as one database operation.
        private int GetSavedCheckInFrequency(
            MySqlConnection con,
            MySqlTransaction transaction)
        {
            const string query = @"
                SELECT frequency_days
                FROM checkin_settings
                WHERE setting_id = 1;";

            using (MySqlCommand cmd = new MySqlCommand(query, con))
            {
                if (transaction != null)
                {
                    cmd.Transaction = transaction;
                }

                object result = cmd.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    return DefaultCheckInFrequencyDays;
                }

                int frequencyDays = Convert.ToInt32(result);

                if (frequencyDays < 1 || frequencyDays > 365)
                {
                    return DefaultCheckInFrequencyDays;
                }

                return frequencyDays;
            }
        }

        [WebMethod(EnableSession = true)]
        public string TestConnection()
        {
            try
            {
                string testQuery = "select * from test";

                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(testQuery, con))
                using (MySqlDataAdapter adapter = new MySqlDataAdapter(cmd))
                {
                    DataTable table = new DataTable();
                    adapter.Fill(table);
                    return "Success!";
                }
            }
            catch (Exception e)
            {
                return "Something went wrong, please check your credentials and db name "
                    + "and try again. Error: " + e.Message;
            }
        }

        // US-07:
        // The existing public method signature stays unchanged, so the current
        // index.html on main does not need to be edited. A persistent anonymous
        // browser cookie is used only for the frequency-control lookup.
        [WebMethod(EnableSession = true)]
        public MoodCheckInResult SubmitMoodCheckIn(
            string mood,
            string workplaceFactor,
            string causeText,
            string recommendationText)
        {
            mood = (mood ?? string.Empty).Trim();
            workplaceFactor = (workplaceFactor ?? string.Empty).Trim();
            causeText = (causeText ?? string.Empty).Trim();
            recommendationText = (recommendationText ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(mood)
                || string.IsNullOrWhiteSpace(workplaceFactor)
                || string.IsNullOrWhiteSpace(causeText)
                || string.IsNullOrWhiteSpace(recommendationText))
            {
                return new MoodCheckInResult
                {
                    Success = false,
                    Message = "Please complete every required field."
                };
            }

            string anonymousToken = GetOrCreateAnonymousCheckInToken();
            string anonymousTokenHash =
                HashAnonymousCheckInToken(anonymousToken);

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                {
                    con.Open();

                    using (MySqlTransaction transaction = con.BeginTransaction())
                    {
                        try
                        {
                            int frequencyDays =
                                GetSavedCheckInFrequency(con, transaction);

                            // Make sure a row exists for this anonymous browser.
                            // For a brand-new browser, seed the timestamp far enough
                            // in the past that the first real submission is eligible.
                            const string ensureFrequencyRowQuery = @"
                                INSERT INTO checkin_frequency_log
                                    (anonymous_token, last_submitted_at)
                                VALUES
                                    (
                                        @anonymousTokenHash,
                                        DATE_SUB(UTC_TIMESTAMP(), INTERVAL 366 DAY)
                                    )
                                ON DUPLICATE KEY UPDATE
                                    last_submitted_at = last_submitted_at;";

                            using (MySqlCommand ensureCmd =
                                new MySqlCommand(ensureFrequencyRowQuery, con))
                            {
                                ensureCmd.Transaction = transaction;
                                ensureCmd.Parameters.Add(
                                    "@anonymousTokenHash",
                                    MySqlDbType.VarChar,
                                    128
                                ).Value = anonymousTokenHash;

                                ensureCmd.ExecuteNonQuery();
                            }

                            // Lock this anonymous-control row while eligibility is
                            // checked so two rapid requests cannot both be accepted.
                            const string lastSubmissionQuery = @"
                                SELECT last_submitted_at
                                FROM checkin_frequency_log
                                WHERE anonymous_token = @anonymousTokenHash
                                FOR UPDATE;";

                            DateTime lastSubmittedUtc;

                            using (MySqlCommand lastCmd =
                                new MySqlCommand(lastSubmissionQuery, con))
                            {
                                lastCmd.Transaction = transaction;
                                lastCmd.Parameters.Add(
                                    "@anonymousTokenHash",
                                    MySqlDbType.VarChar,
                                    128
                                ).Value = anonymousTokenHash;

                                object lastResult = lastCmd.ExecuteScalar();

                                if (lastResult == null || lastResult == DBNull.Value)
                                {
                                    throw new InvalidOperationException(
                                        "The anonymous frequency-control row "
                                        + "could not be loaded.");
                                }

                                lastSubmittedUtc = DateTime.SpecifyKind(
                                    Convert.ToDateTime(lastResult),
                                    DateTimeKind.Utc
                                );
                            }

                            DateTime nextEligibleUtc =
                                lastSubmittedUtc.AddDays(frequencyDays);

                            if (DateTime.UtcNow < nextEligibleUtc)
                            {
                                transaction.Rollback();

                                return new MoodCheckInResult
                                {
                                    Success = false,
                                    Message =
                                        "A check-in from this anonymous browser "
                                        + "was already accepted within the last "
                                        + frequencyDays
                                        + (frequencyDays == 1 ? " day. " : " days. ")
                                        + "Please try again after "
                                        + nextEligibleUtc.ToString(
                                            "MMMM d, yyyy 'at' HH:mm 'UTC'"
                                        )
                                        + "."
                                };
                            }

                            // Anonymous feedback remains in mood_checkins only.
                            // The control-token hash is never written to this table.
                            const string insertCheckInQuery = @"
                                INSERT INTO mood_checkins
                                    (
                                        mood,
                                        workplace_factor,
                                        cause_text,
                                        recommendation_text
                                    )
                                VALUES
                                    (
                                        @mood,
                                        @workplaceFactor,
                                        @causeText,
                                        @recommendationText
                                    );";

                            int rowsAffected;

                            using (MySqlCommand insertCmd =
                                new MySqlCommand(insertCheckInQuery, con))
                            {
                                insertCmd.Transaction = transaction;

                                insertCmd.Parameters.Add(
                                    "@mood",
                                    MySqlDbType.VarChar,
                                    20
                                ).Value = mood;

                                insertCmd.Parameters.Add(
                                    "@workplaceFactor",
                                    MySqlDbType.VarChar,
                                    50
                                ).Value = workplaceFactor;

                                insertCmd.Parameters.Add(
                                    "@causeText",
                                    MySqlDbType.Text
                                ).Value = causeText;

                                insertCmd.Parameters.Add(
                                    "@recommendationText",
                                    MySqlDbType.Text
                                ).Value = recommendationText;

                                rowsAffected = insertCmd.ExecuteNonQuery();
                            }

                            if (rowsAffected != 1)
                            {
                                transaction.Rollback();

                                return new MoodCheckInResult
                                {
                                    Success = false,
                                    Message =
                                        "The check-in could not be recorded. "
                                        + "Please try again."
                                };
                            }

                            // Only after the anonymous feedback insert succeeds do we
                            // move the separate frequency-control timestamp forward.
                            const string updateFrequencyLogQuery = @"
                                UPDATE checkin_frequency_log
                                SET last_submitted_at = UTC_TIMESTAMP()
                                WHERE anonymous_token = @anonymousTokenHash;";

                            using (MySqlCommand updateCmd =
                                new MySqlCommand(updateFrequencyLogQuery, con))
                            {
                                updateCmd.Transaction = transaction;

                                updateCmd.Parameters.Add(
                                    "@anonymousTokenHash",
                                    MySqlDbType.VarChar,
                                    128
                                ).Value = anonymousTokenHash;

                                updateCmd.ExecuteNonQuery();
                            }

                            transaction.Commit();

                            return new MoodCheckInResult
                            {
                                Success = true,
                                Message = "Your anonymous check-in was recorded."
                            };
                        }
                        catch
                        {
                            try
                            {
                                transaction.Rollback();
                            }
                            catch
                            {
                                // Preserve the original exception if rollback fails.
                            }

                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return new MoodCheckInResult
                {
                    Success = false,
                    Message =
                        "The check-in could not be recorded. Please try again."
                };
            }
        }

        [WebMethod(EnableSession = true)]
        public ManagerLoginResult LoginManager(string username, string password)
        {
            username = (username ?? string.Empty).Trim();
            password = (password ?? string.Empty).Trim();

            if (username == "admin" && password == "admin")
            {
                Session["IsManager"] = "true";

                return new ManagerLoginResult
                {
                    Success = true,
                    Message = "Login successful."
                };
            }

            Session.Remove("IsManager");

            return new ManagerLoginResult
            {
                Success = false,
                Message = "Invalid or blank credentials."
            };
        }

        [WebMethod(EnableSession = true)]
        public ManagerLoginResult LogoutManager()
        {
            Session.Remove("IsManager");

            return new ManagerLoginResult
            {
                Success = true,
                Message = "Logged out successfully."
            };
        }

        [WebMethod(EnableSession = true)]
        public List<CheckInRecord> GetRecentCheckIns()
        {
            List<CheckInRecord> checkIns = new List<CheckInRecord>();

            if (!IsManagerSession())
            {
                return checkIns;
            }

            string query =
                "SELECT * FROM mood_checkins ORDER BY checkin_id DESC;";

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    con.Open();

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            checkIns.Add(new CheckInRecord
                            {
                                SubmissionDate =
                                    Convert.ToDateTime(reader["created_at"])
                                        .ToString("MMMM dd, yyyy"),
                                Mood = reader["mood"].ToString(),
                                WorkplaceFactor =
                                    reader["workplace_factor"].ToString(),
                                CauseText = reader["cause_text"].ToString(),
                                RecommendationText =
                                    reader["recommendation_text"].ToString()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return checkIns;
        }

        // US-07:
        // Manager-only service to load the saved company-wide frequency.
        [WebMethod(EnableSession = true)]
        public FrequencySettingResult GetCheckInFrequency()
        {
            if (!IsManagerSession())
            {
                return new FrequencySettingResult
                {
                    Success = false,
                    FrequencyDays = 0,
                    Message = "Manager login is required."
                };
            }

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                {
                    con.Open();

                    int frequencyDays =
                        GetSavedCheckInFrequency(con, null);

                    return new FrequencySettingResult
                    {
                        Success = true,
                        FrequencyDays = frequencyDays,
                        Message = "The saved check-in frequency was loaded."
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return new FrequencySettingResult
                {
                    Success = false,
                    FrequencyDays = DefaultCheckInFrequencyDays,
                    Message =
                        "The current check-in frequency could not be loaded."
                };
            }
        }

        // US-07:
        // Manager-only service to save the company-wide frequency.
        // INSERT ... ON DUPLICATE KEY UPDATE also repairs a missing setting row.
        [WebMethod(EnableSession = true)]
        public FrequencySettingResult SaveCheckInFrequency(int frequencyDays)
        {
            if (!IsManagerSession())
            {
                return new FrequencySettingResult
                {
                    Success = false,
                    FrequencyDays = 0,
                    Message = "Manager login is required."
                };
            }

            if (frequencyDays < 1 || frequencyDays > 365)
            {
                return new FrequencySettingResult
                {
                    Success = false,
                    FrequencyDays = frequencyDays,
                    Message = "Enter a frequency between 1 and 365 days."
                };
            }

            const string query = @"
                INSERT INTO checkin_settings
                    (setting_id, frequency_days)
                VALUES
                    (1, @frequencyDays)
                ON DUPLICATE KEY UPDATE
                    frequency_days = @frequencyDays;";

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.Add(
                        "@frequencyDays",
                        MySqlDbType.Int32
                    ).Value = frequencyDays;

                    con.Open();
                    cmd.ExecuteNonQuery();

                    return new FrequencySettingResult
                    {
                        Success = true,
                        FrequencyDays = frequencyDays,
                        Message = "The check-in frequency was saved."
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

                return new FrequencySettingResult
                {
                    Success = false,
                    FrequencyDays = frequencyDays,
                    Message = "The frequency setting could not be saved."
                };
            }
        }

        [WebMethod(EnableSession = true)]
        public List<ActionUpdateRecord> GetManagementActionUpdates()
        {
            List<ActionUpdateRecord> updates =
                new List<ActionUpdateRecord>();

            string query = @"
                SELECT
                    action_update_id,
                    title,
                    description,
                    status,
                    COALESCE(updated_at, created_at) AS update_date
                FROM management_action_updates
                ORDER BY update_date DESC;";

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    con.Open();

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            updates.Add(new ActionUpdateRecord
                            {
                                ActionUpdateId =
                                    Convert.ToInt32(
                                        reader["action_update_id"]
                                    ),
                                Title = reader["title"].ToString(),
                                Description =
                                    reader["description"].ToString(),
                                Status = reader["status"].ToString(),
                                UpdateDate =
                                    Convert.ToDateTime(
                                        reader["update_date"]
                                    ).ToString("MMMM dd, yyyy")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Unable to retrieve management action updates.",
                    ex
                );
            }

            return updates;
        }

        // US-10: Get Dashboard Summary Method
        [WebMethod(EnableSession = true)]
        public DashboardSummary GetDashboardSummary()
        {
            DashboardSummary summary = new DashboardSummary
            {
                Moods = new List<MoodSummary>(),
                Factors = new List<FactorSummary>()
            };

            if (!IsManagerSession())
            {
                return summary;
            }

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                {
                    con.Open();

                    string moodQuery =
                        "SELECT mood, COUNT(*) as count "
                        + "FROM mood_checkins GROUP BY mood;";

                    using (MySqlCommand cmd =
                        new MySqlCommand(moodQuery, con))
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            summary.Moods.Add(new MoodSummary
                            {
                                Mood = reader["mood"].ToString(),
                                Count = Convert.ToInt32(reader["count"])
                            });
                        }
                    }

                    string factorQuery =
                        "SELECT workplace_factor, COUNT(*) as count "
                        + "FROM mood_checkins GROUP BY workplace_factor;";

                    using (MySqlCommand cmd =
                        new MySqlCommand(factorQuery, con))
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            summary.Factors.Add(new FactorSummary
                            {
                                Factor =
                                    reader["workplace_factor"].ToString(),
                                Count = Convert.ToInt32(reader["count"])
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return summary;
        }
    }

    public class ManagerLoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    // US-07 result object used by manager frequency load/save calls.
    public class FrequencySettingResult
    {
        public bool Success { get; set; }
        public int FrequencyDays { get; set; }
        public string Message { get; set; }
    }

    public class ActionUpdateRecord
    {
        public int ActionUpdateId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public string UpdateDate { get; set; }
    }

    public class CheckInRecord
    {
        public string SubmissionDate { get; set; }
        public string Mood { get; set; }
        public string WorkplaceFactor { get; set; }
        public string CauseText { get; set; }
        public string RecommendationText { get; set; }
    }

    public class MoodSummary
    {
        public string Mood { get; set; }
        public int Count { get; set; }
    }

    public class FactorSummary
    {
        public string Factor { get; set; }
        public int Count { get; set; }
    }

    public class DashboardSummary
    {
        public List<MoodSummary> Moods { get; set; }
        public List<FactorSummary> Factors { get; set; }
    }
}