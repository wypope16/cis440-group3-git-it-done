using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Data;
using System.Globalization;

namespace ProjectTemplate
{
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    [System.Web.Script.Services.ScriptService]
    public class ProjectServices : System.Web.Services.WebService
    {
        ////////////////////////////////////////////////////////////////////////
        ///replace the values of these variables with your database credentials
        ////////////////////////////////////////////////////////////////////////
        private string dbID = "cis440sum26team3";
        private string dbPass = "cis440sum26team3";
        private string dbName = "cis440sum26team3";
        ////////////////////////////////////////////////////////////////////////

        ////////////////////////////////////////////////////////////////////////
        ///call this method anywhere that you need the connection string!
        ////////////////////////////////////////////////////////////////////////
        private string getConString()
        {
            return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName + "; UID=" + dbID + "; PASSWORD=" + dbPass;
        }
        ////////////////////////////////////////////////////////////////////////

        [WebMethod(EnableSession = true)]
        public string TestConnection()
        {
            try
            {
                string testQuery = "select * from test";
                MySqlConnection con = new MySqlConnection(getConString());
                MySqlCommand cmd = new MySqlCommand(testQuery, con);
                MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
                DataTable table = new DataTable();
                adapter.Fill(table);
                return "Success!";
            }
            catch (Exception e)
            {
                return "Something went wrong, please check your credentials and db name and try again.  Error: " + e.Message;
            }
        }

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

            if (string.IsNullOrWhiteSpace(mood) ||
                string.IsNullOrWhiteSpace(workplaceFactor) ||
                string.IsNullOrWhiteSpace(causeText) ||
                string.IsNullOrWhiteSpace(recommendationText))
            {
                return new MoodCheckInResult
                {
                    Success = false,
                    Message = "Please complete every required field."
                };
            }

            const string query = @"
                INSERT INTO mood_checkins
                    (mood, workplace_factor, cause_text, recommendation_text)
                VALUES
                    (@mood, @workplaceFactor, @causeText, @recommendationText);";

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                using (MySqlCommand cmd = new MySqlCommand(query, con))
                {
                    cmd.Parameters.Add("@mood", MySqlDbType.VarChar, 20).Value = mood;
                    cmd.Parameters.Add("@workplaceFactor", MySqlDbType.VarChar, 50).Value = workplaceFactor;
                    cmd.Parameters.Add("@causeText", MySqlDbType.Text).Value = causeText;
                    cmd.Parameters.Add("@recommendationText", MySqlDbType.Text).Value = recommendationText;

                    con.Open();

                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected == 1)
                    {
                        return new MoodCheckInResult
                        {
                            Success = true,
                            Message = "Your anonymous check-in was recorded."
                        };
                    }

                    return new MoodCheckInResult
                    {
                        Success = false,
                        Message = "The check-in could not be recorded. Please try again."
                    };
                }
            }
            catch (Exception)
            {
                return new MoodCheckInResult
                {
                    Success = false,
                    Message = "The check-in could not be recorded. Please try again."
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
    
        // US-13: Allow an authorized manager to post a management action update.
        [WebMethod(EnableSession = true)]
        public bool PostManagementActionUpdate(
    string title,
    string description,
    string status)
        {
            // Only an authenticated manager can create an action update.
            if (Session["IsManager"] == null ||
                !Session["IsManager"].ToString().Equals(
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Clean the values received from the manager dashboard.
            title = (title ?? string.Empty).Trim();
            description = (description ?? string.Empty).Trim();
            status = (status ?? string.Empty).Trim();

            // Required fields cannot be blank.
            if (string.IsNullOrWhiteSpace(title) ||
                string.IsNullOrWhiteSpace(description) ||
                string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            // Only allow the four statuses defined for management action updates.
            bool validStatus =
                status == "Concern Received" ||
                status == "Under Review" ||
                status == "Improvement Planned" ||
                status == "Action Completed";

            if (!validStatus)
            {
                return false;
            }

            string query =
                @"INSERT INTO management_action_updates
          (title, description, status, created_at, updated_at)
          VALUES
          (@title, @description, @status, NOW(), NOW());";

            try
            {
                using (MySqlConnection con =
                    new MySqlConnection(getConString()))
                using (MySqlCommand cmd =
                    new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue(
                        "@title",
                        title);

                    cmd.Parameters.AddWithValue(
                        "@description",
                        description);

                    cmd.Parameters.AddWithValue(
                        "@status",
                        status);

                    con.Open();

                    int rowsAffected =
                        cmd.ExecuteNonQuery();

                    return rowsAffected == 1;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        // US-13: Allow an authorized manager to change
        // the status of an existing management action update.
        [WebMethod(EnableSession = true)]
        public bool UpdateManagementActionStatus(
            int actionUpdateId,
            string status)
        {
            // Only an authenticated manager can change an action update.
            if (Session["IsManager"] == null ||
                !Session["IsManager"].ToString().Equals(
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Clean the status value received from the manager dashboard.
            status = (status ?? string.Empty).Trim();

            // A valid record ID and status are required.
            if (actionUpdateId <= 0 ||
                string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            // Only allow the four statuses defined for US-13.
            bool validStatus =
                status == "Concern Received" ||
                status == "Under Review" ||
                status == "Improvement Planned" ||
                status == "Action Completed";

            if (!validStatus)
            {
                return false;
            }

            // Update only the record matching the supplied primary key.
            string query =
                @"UPDATE management_action_updates
          SET status = @status,
              updated_at = NOW()
          WHERE action_update_id = @actionUpdateId;";

            try
            {
                using (MySqlConnection con =
                    new MySqlConnection(getConString()))
                using (MySqlCommand cmd =
                    new MySqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue(
                        "@status",
                        status);

                    cmd.Parameters.AddWithValue(
                        "@actionUpdateId",
                        actionUpdateId);

                    con.Open();

                    int rowsAffected =
                        cmd.ExecuteNonQuery();

                    return rowsAffected == 1;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        [WebMethod(EnableSession = true)]
        public List<CheckInRecord> GetRecentCheckIns()
        {
            List<CheckInRecord> checkIns = new List<CheckInRecord>();

            if (Session["IsManager"] == null ||
                !Session["IsManager"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return checkIns;
            }

            // Querying the database and ordering by checkin_id descending so the newest check-ins appear first
            string query = "SELECT * FROM mood_checkins ORDER BY checkin_id DESC;";

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
                                SubmissionDate = Convert.ToDateTime(reader["created_at"]).ToString("MMMM dd, yyyy"),
                                Mood = reader["mood"].ToString(),
                                WorkplaceFactor = reader["workplace_factor"].ToString(),
                                CauseText = reader["cause_text"].ToString(),
                                RecommendationText = reader["recommendation_text"].ToString()
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

        [WebMethod(EnableSession = true)]
        public List<ActionUpdateRecord> GetManagementActionUpdates()
        {
            List<ActionUpdateRecord> updates = new List<ActionUpdateRecord>();

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
                                ActionUpdateId = Convert.ToInt32(reader["action_update_id"]),
                                Title = reader["title"].ToString(),
                                Description = reader["description"].ToString(),
                                Status = reader["status"].ToString(),
                                UpdateDate = Convert.ToDateTime(reader["update_date"]).ToString("MMMM dd, yyyy")
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unable to retrieve management action updates.", ex);
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

            // Applying the exact same US-08 security check that Wyatt added above
            if (Session["IsManager"] == null ||
                !Session["IsManager"].ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return summary;
            }

            try
            {
                using (MySqlConnection con = new MySqlConnection(getConString()))
                {
                    con.Open();

                    // Query 1: Count the moods
                    string moodQuery = "SELECT mood, COUNT(*) as count FROM mood_checkins GROUP BY mood;";

                    using (MySqlCommand cmd = new MySqlCommand(moodQuery, con))
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

                    // Query 2: Count the workplace factors
                    string factorQuery = "SELECT workplace_factor, COUNT(*) as count FROM mood_checkins GROUP BY workplace_factor;";

                    using (MySqlCommand cmd = new MySqlCommand(factorQuery, con))
                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            summary.Factors.Add(new FactorSummary
                            {
                                Factor = reader["workplace_factor"].ToString(),
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
        private void AddTrendParameters(
    MySqlCommand cmd,
    string mood,
    string workplaceFactor,
    DateTime? startDateValue,
    DateTime? endDateExclusive)
        {
            cmd.Parameters.Add("@mood", MySqlDbType.VarChar, 20).Value =
                mood;

            cmd.Parameters.Add("@workplaceFactor", MySqlDbType.VarChar, 50).Value =
                workplaceFactor;

            cmd.Parameters.Add("@startDate", MySqlDbType.DateTime).Value =
                startDateValue.HasValue
                    ? (object)startDateValue.Value
                    : DBNull.Value;

            cmd.Parameters.Add("@endDateExclusive", MySqlDbType.DateTime).Value =
                endDateExclusive.HasValue
                    ? (object)endDateExclusive.Value
                    : DBNull.Value;
        }

        // US-12: Return mood and workplace-factor trends
        [WebMethod(EnableSession = true)]
        public DashboardTrendResult GetDashboardTrends(
            string mood,
            string workplaceFactor,
            string startDate,
            string endDate)
        {
            DashboardTrendResult result = new DashboardTrendResult
            {
                Success = false,
                Authorized = false,
                Message = string.Empty,
                MoodTrends = new List<TrendPoint>(),
                FactorTrends = new List<TrendPoint>()
            };

            if (Session["IsManager"] == null ||
                !Session["IsManager"].ToString().Equals(
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                result.Message = "Your manager session has expired.";
                return result;
            }

            result.Authorized = true;

            mood = (mood ?? string.Empty).Trim();
            workplaceFactor = (workplaceFactor ?? string.Empty).Trim();
            startDate = (startDate ?? string.Empty).Trim();
            endDate = (endDate ?? string.Empty).Trim();

            DateTime parsedDate;
            DateTime? startDateValue = null;
            DateTime? endDateExclusive = null;

            if (!string.IsNullOrWhiteSpace(startDate))
            {
                if (!DateTime.TryParseExact(
                    startDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsedDate))
                {
                    result.Message = "The selected start date is invalid.";
                    return result;
                }

                startDateValue = parsedDate.Date;
            }

            if (!string.IsNullOrWhiteSpace(endDate))
            {
                if (!DateTime.TryParseExact(
                    endDate,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsedDate))
                {
                    result.Message = "The selected end date is invalid.";
                    return result;
                }

                // Using the following day makes the selected end date inclusive.
                endDateExclusive = parsedDate.Date.AddDays(1);
            }

            if (startDateValue.HasValue &&
                endDateExclusive.HasValue &&
                startDateValue.Value >= endDateExclusive.Value)
            {
                result.Message =
                    "The start date cannot be later than the end date.";

                return result;
            }

            const string moodQuery = @"
                SELECT
                    DATE(created_at) AS trend_date,
                    mood AS category,
                    COUNT(*) AS count
                FROM mood_checkins
                WHERE
                    (@mood = '' OR mood = @mood)
                    AND
                    (@workplaceFactor = '' OR
                        workplace_factor = @workplaceFactor)
                    AND
                    (@startDate IS NULL OR created_at >= @startDate)
                    AND
                    (@endDateExclusive IS NULL OR
                        created_at < @endDateExclusive)
                GROUP BY DATE(created_at), mood
                ORDER BY trend_date ASC, category ASC;";

            const string factorQuery = @"
                SELECT
                    DATE(created_at) AS trend_date,
                    workplace_factor AS category,
                    COUNT(*) AS count
                FROM mood_checkins
                WHERE
                    (@mood = '' OR mood = @mood)
                    AND
                    (@workplaceFactor = '' OR
                        workplace_factor = @workplaceFactor)
                    AND
                    (@startDate IS NULL OR created_at >= @startDate)
                    AND
                    (@endDateExclusive IS NULL OR
                        created_at < @endDateExclusive)
                GROUP BY DATE(created_at), workplace_factor
                ORDER BY trend_date ASC, category ASC;";

            try
            {
                using (MySqlConnection con =
                    new MySqlConnection(getConString()))
                {
                    con.Open();

                    using (MySqlCommand cmd =
                        new MySqlCommand(moodQuery, con))
                    {
                        AddTrendParameters(
                            cmd,
                            mood,
                            workplaceFactor,
                            startDateValue,
                            endDateExclusive);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.MoodTrends.Add(new TrendPoint
                                {
                                    Date = Convert.ToDateTime(
                                        reader["trend_date"])
                                        .ToString("yyyy-MM-dd"),

                                    Category =
                                        reader["category"].ToString(),

                                    Count =
                                        Convert.ToInt32(reader["count"])
                                });
                            }
                        }
                    }

                    using (MySqlCommand cmd =
                        new MySqlCommand(factorQuery, con))
                    {
                        AddTrendParameters(
                            cmd,
                            mood,
                            workplaceFactor,
                            startDateValue,
                            endDateExclusive);

                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.FactorTrends.Add(new TrendPoint
                                {
                                    Date = Convert.ToDateTime(
                                        reader["trend_date"])
                                        .ToString("yyyy-MM-dd"),

                                    Category =
                                        reader["category"].ToString(),

                                    Count =
                                        Convert.ToInt32(reader["count"])
                                });
                            }
                        }
                    }
                }

                result.Success = true;
                result.Message = "Trend data loaded successfully.";
            }
            catch (Exception)
            {
                result.Message =
                    "Unable to load dashboard trends. Please try again.";
            }

            return result;
        }
    }

    public class ManagerLoginResult
    {
        public bool Success { get; set; }
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

    // This class organizes the data before sending it securely to the frontend
    public class CheckInRecord
    {
        public string SubmissionDate { get; set; }
        public string Mood { get; set; }
        public string WorkplaceFactor { get; set; }
        public string CauseText { get; set; }
        public string RecommendationText { get; set; }
    }

    // US-10: Dashboard Summary Classes
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

    // US-12: Dashboard trend response classes
    public class TrendPoint
    {
        public string Date { get; set; }
        public string Category { get; set; }
        public int Count { get; set; }
    }

    public class DashboardTrendResult
    {
        public bool Success { get; set; }
        public bool Authorized { get; set; }
        public string Message { get; set; }
        public List<TrendPoint> MoodTrends { get; set; }
        public List<TrendPoint> FactorTrends { get; set; }
    }
}
