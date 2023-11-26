using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Sbt.Models;
using System.Net;

namespace Sbt.Data
{
    public class DemoContext : DbContext
    {
        public record LoadScheduleResult
        {
            public bool Success { get; init; }
            public string ErrorMessage { get; init; } = string.Empty;
            public DateTime FirstGameDate { get; init; }
            public DateTime LastGameDate { get; init; }
        };

        public class DivisionExistsException : Exception { }

        private readonly string _containerName = "organizations";
        private readonly string _divisionListID = "DivisionListID";

        public DbSet<Sbt.Models.Division> Divisions { get; set; } = default!;
        public DbSet<Sbt.Models.DivisionInfoList> DivisionInfoSet { get; set; } = default!;

        public DemoContext(DbContextOptions<DemoContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultContainer(this._containerName);

            modelBuilder.Entity<DivisionInfoList>().HasPartitionKey(d => d.Organization);
            modelBuilder.Entity<Division>().HasPartitionKey(d => d.Organization);
        }

        #region Data Access Layer Methods
        public async Task<Division> GetDivision(string organization, string divisionID)
        {
            try
            {
                var division = await this.Divisions
                    .WithPartitionKey(organization)
                    .Where(d => d.Organization == organization && d.ID == divisionID.ToLower())
                    .FirstOrDefaultAsync();

                if (division != null)
                    return division;
                else
                    return new Division();
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected Exception: " + ex.Message);
            }
        }

        public async Task<(DivisionInfoList?, DivisionInfo?)> GetDivisionInfoIfExists(string organization, string divisionID)
        {
            try
            {
                var divInfoList = await this.DivisionInfoSet
                    .WithPartitionKey(organization)
                    .Where(d => d.Organization == organization && d.ID == this._divisionListID)
                    .FirstOrDefaultAsync();

                if (divInfoList == null)
                {
                    return (null, null);
                }
                if (divInfoList.DivisionList == null)
                {
                    return (divInfoList, null);
                }
                var divInfo = divInfoList.DivisionList
                    .FirstOrDefault(d => d.Organization == organization && d.ID.ToLower() == divisionID.ToLower());

                return (divInfoList, divInfo);
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected Exception: " + ex.Message);
            }
        }

        public async Task<List<DivisionInfo>> GetDivisionList(string organization)
        {
            try
            {
                (var infoSet, _) = await this.GetDivisionInfoIfExists(organization, string.Empty);
                if (infoSet == null || infoSet.DivisionList == null)
                {
                    // No divisions - return empty list
                    return new List<DivisionInfo>();
                }

                return infoSet.DivisionList;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // No divisions - return empty list
                return new List<DivisionInfo>();
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected Exception: " + ex.Message);
            }
        }

        public async Task<List<Schedule>> GetGames(string organization, string divisionID, int gameID)
        {
            List<Schedule> list = new List<Schedule>();

            try
            {
                // Step 1: do a query returning 1 game result based on the game id
                // Step 2: do a second query using that game's day and field
                //var schedule = await this.Divisions
                //    .Where(d => d.Organization == organization && d.ID == divisionID.ToLower())
                //    .SelectMany(d => d.Schedule)
                //    .Where(s => s.GameID == gameID)
                //    .FirstOrDefaultAsync();

                // ef core could not handle the query above (threw exception about inability to translate)
                // so now we just get the entire division and query the schedule list directly

                var division = await this.GetDivision(organization, divisionID);

                var games = division.Schedule
                    .Where(s => s.GameID == gameID)
                    .SelectMany(s => division.Schedule.Where(inner => inner.Day == s.Day && inner.Field == s.Field))
                    .ToList();

                if (games != null)
                {
                    foreach (var game in games)
                    {
                        list.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected Exception: " + ex.Message);
            }

            return list;
        }

        public async Task<LoadScheduleResult> LoadScheduleFileAsync(IFormFile scheduleFile, string organization, string divisionID,
        bool usesDoubleHeaders)
        {
            // todo - break this method up into smaller methods
            // leaving it alone for this version to make it easy to compare code
            // between Cosmos SDK and EF Core version

            bool docuumentExists = true;
            string errorMessage = string.Empty;
            DateTime firstGameDate = DateTime.MinValue;
            DateTime lastGameDate = DateTime.MinValue;
            int gameID = 0; // NOTE - Game IDs are unique within a Division (document) for Cosmos
            List<string> lines = new();
            Division? division;
            var standings = new List<Standings>();
            var schedule = new List<Schedule>();

            (_, var divisionInfo) = await this.GetDivisionInfoIfExists(organization, divisionID);
            
            if (divisionInfo == null)
            {
                return new LoadScheduleResult
                {
                    Success = false,
                    ErrorMessage = "Division Does Not Exist",
                    FirstGameDate = firstGameDate,
                    LastGameDate = lastGameDate
                };
            }

            // we know division exists in InfoList but it may not have its own document yet
            try
            {
                division = await this.Divisions
                    .WithPartitionKey(organization)
                    .FirstOrDefaultAsync(m => m.Organization == organization && m.ID == divisionID.ToLower());
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected Exception: " + ex.Message);
            }

            if(division == null)
            {
                // this is okay, document for division doesn't exists so create it
                division = new Division();
                division.Organization = organization;
                division.ID = divisionID.ToLower();
                docuumentExists = false;
            }

            using (var reader = new StreamReader(scheduleFile.OpenReadStream()))
            {
                while (reader.Peek() >= 0)
                    lines.Add(reader.ReadLine()!);
            }

            try
            {
                // Note - expecting a properly formatted file since it is self-created,
                // solely for the purposes of populating some demo data for the website.
                // therefore no error-checking is done here - just wrapping in try-catch
                // and returning exceptions to the calling method

                List<string> teams = new();
                int lineNumber = 0;
                short teamID = 1;

                // skip first 4 lines which are simply for ease of reading the file
                lineNumber = 4;

                // next lines are teams - ended by blank line
                // team IDs are assumed, starting at 1
                while (lines[lineNumber].Length > 0)
                {
                    teams.Add(lines[lineNumber].Trim());

                    // create standings row for each team
                    var standingsRow = new Standings
                    {
                        Wins = 0,
                        Losses = 0,
                        Ties = 0,
                        OvertimeLosses = 0,
                        Percentage = 0,
                        GB = 0,
                        RunsAgainst = 0,
                        RunsScored = 0,
                        Forfeits = 0,
                        ForfeitsCharged = 0,
                        Name = lines[lineNumber].Trim(),
                        TeamID = teamID++
                    };
                    standings.Add(standingsRow);
                    lineNumber++;
                }

                // rest of file is the actual schedule, in this format:
                // Date,Day,Time,Home,Visitor,Field
                for (int index = lineNumber + 1; index < lines.Count; index++)
                {
                    string[] data = lines[index].Split(',');

                    if (data[0].ToLower().StartsWith("week"))
                    {
                        // original code had complicated method to determine week boundaries,
                        // but for simplicity's sake I am adding this info in the schedule files
                        schedule.Add(this.AddWeekBoundary(data[0], gameID));
                        gameID++;
                        continue;
                    }
                    DateTime gameDate = DateTime.Parse(data[0]);
                    // skipping value at [1] - not currently used in this version of the website
                    DateTime gameTime = DateTime.Parse(data[2]);
                    short homeTeamID = short.Parse(data[3]);
                    short visitorTeamID = short.Parse(data[4]);
                    string field = data[5];

                    // create schedule row for each game
                    var scheduleRow = new Schedule
                    {
                        GameID = gameID++,
                        Day = gameDate,
                        Field = field,
                        Home = teams[homeTeamID - 1],
                        HomeForfeit = false,
                        HomeID = homeTeamID,
                        Time = gameTime,
                        Visitor = teams[visitorTeamID - 1],
                        VisitorForfeit = false,
                        VisitorID = visitorTeamID,
                    };
                    schedule.Add(scheduleRow);

                    if (usesDoubleHeaders)
                    {
                        // add a second game 90 minutes later, swapping home/visitor
                        scheduleRow = new Schedule
                        {
                            GameID = gameID++,
                            Day = gameDate,
                            Field = field,
                            Home = teams[visitorTeamID - 1],
                            HomeForfeit = false,
                            HomeID = visitorTeamID,
                            Time = gameTime.AddMinutes(90),
                            Visitor = teams[homeTeamID - 1],
                            VisitorForfeit = false,
                            VisitorID = homeTeamID,
                        };
                        schedule.Add(scheduleRow);
                    }

                    // keep track of first and last games to show when done processing file,
                    // as a way to show user that the entire schedule was processed.
                    if (index == lineNumber + 2)
                    {
                        firstGameDate = gameDate;
                    }
                    else if (index == lines.Count - 1)
                    {
                        lastGameDate = gameDate;
                    }
                } // for loop processing schedule data

                division.Schedule = schedule;
                division.Standings = standings;

                if (docuumentExists)
                {
                    // ef core is tracking this, nothing to do here
                }
                else
                {
                    this.Divisions.Add(division);
                }

                divisionInfo.Updated = this.GetEasternTime();
                await this.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    errorMessage = ex.Message + ":<br>" + ex.InnerException.Message;
                }
                else
                {
                    errorMessage = ex.Message;
                }
            }

            return new LoadScheduleResult
            {
                Success = (errorMessage == string.Empty) ? true : false,
                ErrorMessage = errorMessage,
                FirstGameDate = firstGameDate,
                LastGameDate = lastGameDate
            };
        }

        public async Task SaveDivisionInfo(DivisionInfo divisionInfo, 
            bool deleteDivision = false, bool createDivision = false)
        {
            try
            {
                var org = divisionInfo.Organization;
                var id = divisionInfo.ID.ToLower();

                (var infoSet, var tmpDivisionInfo) = await this.GetDivisionInfoIfExists(org, id);

                if (infoSet == null)
                {
                    // create new document for division info list
                    infoSet = new DivisionInfoList();
                    infoSet.Organization = org;
                    infoSet.ID = this._divisionListID;
                    infoSet.DivisionList = new List<DivisionInfo>();
                    this.DivisionInfoSet.Add(infoSet);
                }
                else if (infoSet.DivisionList == null)
                {
                    // create new list inside document
                    infoSet.DivisionList = new List<DivisionInfo>();
                }

                if (tmpDivisionInfo != null)
                {
                    // throw error if creating a division that already exists
                    if (createDivision == true)
                    {
                        throw new DivisionExistsException();
                    }

                    // it is okay to delete pre-existing division info for edit or delete
                    infoSet.DivisionList.Remove(tmpDivisionInfo);
                }

                if (deleteDivision)
                {
                    // also need to remove division document
                    var divisions = await this.Divisions
                        .WithPartitionKey(org)
                        .Where(d => d.Organization == org && d.ID == id)
                        .FirstOrDefaultAsync();

                    if (divisions != null)
                    {
                        this.Divisions.Remove(divisions);
                    }
                }
                else // edit or create
                {
                    divisionInfo.Updated = this.GetEasternTime();
                    infoSet.DivisionList.Add(divisionInfo);
                }

                await this.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected Exception: " + ex.Message);
            }
        }

        public async Task SaveScores(string organization, string divisionID, IList<ScheduleVM> schedules)
        {
            try
            {
                var division = await this.GetDivision(organization, divisionID);

                for (int i = 0; i < schedules.Count; i++)
                {
                    // find matching game id
                    var gameToUpdate = division.Schedule.FirstOrDefault(s => s.GameID == schedules[i].GameID);

                    if (gameToUpdate != null)
                    {
                        // populate Model from ViewModel (which is used to prevent overposting)
                        gameToUpdate.HomeForfeit = schedules[i].HomeForfeit;
                        gameToUpdate.HomeScore = schedules[i].HomeScore;
                        gameToUpdate.VisitorForfeit = schedules[i].VisitorForfeit;
                        gameToUpdate.VisitorScore = schedules[i].VisitorScore;

                        // force forfeit scores in case they came in wrong
                        if (gameToUpdate.VisitorForfeit)
                        {
                            gameToUpdate.VisitorScore = 0;
                            gameToUpdate.HomeScore = (gameToUpdate.HomeForfeit) ? (short)0 : (short)7;
                        }
                        else if (gameToUpdate.HomeForfeit)
                        {
                            gameToUpdate.VisitorScore = 7;
                            gameToUpdate.HomeScore = 0;
                        }
                    }
                }

                this.ReCalcStandings(division);

                // division updated time changes when scores are reported
                (_, var divisionInfo) = await this.GetDivisionInfoIfExists(organization, divisionID);
                if ((divisionInfo != null))
                {
                    divisionInfo.Updated = this.GetEasternTime();
                }
            
                await this.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Unexpected Exception: " + ex.Message);
            }
        }
        #endregion

        #region Helper Methods
        private void ReCalcStandings(Division division)
        {
            var standings = division.Standings;

            var schedule = division.Schedule;

            // zero-out standings
            foreach (var stand in standings)
            {
                stand.Forfeits = stand.Losses = stand.OvertimeLosses = stand.Ties = stand.Wins = 0;
                stand.RunsAgainst = stand.RunsScored = stand.ForfeitsCharged = 0;
                stand.GB = stand.Percentage = 0;
            }

            foreach (var sched in schedule)
            {
                // skip week boundary
                if (sched.Visitor.ToUpper().StartsWith("WEEK") == true) continue;

                this.UpdateStandings(standings, sched);
            }
        }

        private void UpdateStandings(List<Standings> standings, Schedule sched)
        {
            // note - IList starts at 0, team IDs start at 1
            var homeTteam = standings[sched.HomeID - 1];
            var visitorTeam = standings[sched.VisitorID - 1];

            if (sched.HomeScore > -1) // this will catch null values (no scores reported yet)
            {
                homeTteam.RunsScored += (short)sched.HomeScore!;
                homeTteam.RunsAgainst += (short)sched.VisitorScore!;
                visitorTeam.RunsScored += (short)sched.VisitorScore!;
                visitorTeam.RunsAgainst += (short)sched.HomeScore!;
            }

            if (sched.HomeForfeit)
            {
                homeTteam.Forfeits++;
                homeTteam.ForfeitsCharged++;
            }
            if (sched.VisitorForfeit)
            {
                visitorTeam.Forfeits++;
                visitorTeam.ForfeitsCharged++;
            }

            if (sched.VisitorForfeit && sched.HomeForfeit)
            {
                // special case - not a tie - counted as losses for both team
                homeTteam.Losses++;
                visitorTeam.Losses++;
            }
            else if (sched.HomeScore > sched.VisitorScore)
            {
                homeTteam.Wins++;
                visitorTeam.Losses++;
            }
            else if (sched.HomeScore < sched.VisitorScore)
            {
                homeTteam.Losses++;
                visitorTeam.Wins++;
            }
            else if (sched.HomeScore > -1) // this will catch null values (no scores reported yet)
            {
                homeTteam.Ties++;
                visitorTeam.Ties++;
            }

            // calculate Games Behind (GB)
            var sortedTeams = standings.OrderByDescending(t => t.Wins).ToList();
            var maxWins = sortedTeams.First().Wins;
            var maxLosses = sortedTeams.First().Losses;
            foreach (var team in sortedTeams)
            {
                team.GB = ((maxWins - team.Wins) + (team.Losses - maxLosses)) / 2.0f;
                if ((team.Wins + team.Losses) == 0)
                {
                    team.Percentage = 0.0f;
                }
                else
                {
                    team.Percentage = (float)team.Wins / (team.Wins + team.Losses + team.Ties);
                }
            }
        }

        private DateTime GetEasternTime()
        {
            DateTime utcTime = DateTime.UtcNow;

            TimeZoneInfo easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, easternTimeZone);
        }

        private Schedule AddWeekBoundary(string week, int maxGameID)
        {
            // this creates a mostly empty "WEEK #" row to make it easier to show
            // week boundaries when displaying the schedule.
            var scheduleRow = new Schedule
            {
                GameID = maxGameID,
                HomeForfeit = false,
                Visitor = week,
                VisitorForfeit = false,
            };

            return scheduleRow;
        }
        #endregion
    }
}
