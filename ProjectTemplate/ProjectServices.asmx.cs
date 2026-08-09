using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Data;

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
}
