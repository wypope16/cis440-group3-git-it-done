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
		private string getConString() {
			return "SERVER=107.180.1.16; PORT=3306; DATABASE=" + dbName+"; UID=" + dbID + "; PASSWORD=" + dbPass;
		}
		////////////////////////////////////////////////////////////////////////



		/////////////////////////////////////////////////////////////////////////
		//don't forget to include this decoration above each method that you want
		//to be exposed as a web service!
		
		[WebMethod(EnableSession = true)]
		/////////////////////////////////////////////////////////////////////////
		public string TestConnection()
		{
			try
			{
				string testQuery = "select * from test";

				////////////////////////////////////////////////////////////////////////
				///here's an example of using the getConString method!
				////////////////////////////////////////////////////////////////////////
				MySqlConnection con = new MySqlConnection(getConString());
				////////////////////////////////////////////////////////////////////////

				MySqlCommand cmd = new MySqlCommand(testQuery, con);
				MySqlDataAdapter adapter = new MySqlDataAdapter(cmd);
				DataTable table = new DataTable();
				adapter.Fill(table);
				return "Success!";
			}
			catch (Exception e)
			{
				return "Something went wrong, please check your credentials and db name and try again.  Error: "+e.Message;
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
                    cmd.Parameters.Add("@workplaceFactor", MySqlDbType.VarChar, 50).Value =
                        workplaceFactor;
                    cmd.Parameters.Add("@causeText", MySqlDbType.Text).Value = causeText;
                    cmd.Parameters.Add("@recommendationText", MySqlDbType.Text).Value =
                        recommendationText;

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

            // Querying the database and ordering by ID descending so the newest check-ins appear first
            string query = "SELECT * FROM mood_checkins ORDER BY id DESC;";

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
                                // Formatting the timestamp for a clean dashboard view
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
                // If the connection fails, it will return an empty list and trigger the UI's error message
                Console.WriteLine(ex.Message);
            }

            return checkIns;
        }
    }
public class ManagerLoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
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
}
