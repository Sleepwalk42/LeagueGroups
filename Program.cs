using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LeagueGroups
{
    class Program
    {
        static void Main(string[] args)
        {
            var result = Parser.Default.ParseArguments<Options>(args);

            var players = ReadPlayerList(result.Value.Players);

            var priorWeeks = new List<Week>();

            foreach (string weekFile in result.Value.PriorWeeks)
            {
                priorWeeks.Add(Week.ReadFromFile(weekFile));
            }

            GroupCreator creator;
            Week newWeek;

            do
            {
                creator = new GroupCreator(players, priorWeeks);
                newWeek = creator.CreateWeek();
            } while (creator.MaxEncounters > 0);

            if (result.Value.Output == null)
                newWeek.WriteToConsole();
            else
                newWeek.WriteToFile(result.Value.Output);
        }

        static List<string> ReadPlayerList(string path)
        {
            var players = new List<string>(File.ReadAllLines(path));
            players.ForEach(line => line = line.Trim());
            players.RemoveAll(line => string.IsNullOrEmpty(line) || line.StartsWith(';'));
            return players;
        }       
    }

    public class Options
    {
        [Option('p', HelpText = "The path to the file containing the players for the week to be generated", Required = true)]
        public string Players { get; set; }

        [Option('o', HelpText = "The path to the file where the output will be written", Required = false)]
        public string Output { get; set; }

        [Option('w', HelpText = "The paths to the files containing data about the prior weeks", Required = false)]
        public IEnumerable<string> PriorWeeks { get; set; }
    }

    public class GroupCreator
    {
        private HashSet<string> _players;
        private List<Week> _priorWeeks;
        private HashSet<string> _ungroupedPlayers;
        private static readonly Random _random = new Random();

        public GroupCreator(List<string> players, List<Week> priorWeeks)
        {
            _players = new HashSet<string>(players);
            _priorWeeks = priorWeeks;
            _ungroupedPlayers = new HashSet<string>(players);
        }

        public Week CreateWeek()
        {
            var newWeek = new Week();

            for (int i = 0; i < GroupCount; i++)
            {
                if (i < NumberOfFourPlayerGroups)
                    newWeek.Groups.Add(CreateGroup(4));
                else
                    newWeek.Groups.Add(CreateGroup(3));
            }
            return newWeek;
        }

        private Week.Group CreateGroup(int groupSize)
        {
            var group = new Week.Group();
            string firstPlayer = _ungroupedPlayers.ElementAt(_random.Next(_ungroupedPlayers.Count));
            group.Players.Add(firstPlayer);
            _ungroupedPlayers.Remove(firstPlayer);

            for (int i = 0; i < groupSize - 1; i++)
            {
                var newPlayer = GetPlayerForGroup(group);
                group.Players.Add(newPlayer);
                _ungroupedPlayers.Remove(newPlayer);
            }
            return group;
        }

        private string GetPlayerForGroup(Week.Group group)
        {
            List<Dictionary<string, int>> allPreviousPlayers = new();
            foreach (string player in group.Players)
                allPreviousPlayers.Add(PreviousPlayers(player));

            string newPlayer = null;
            int maxEncounters = 0;
            IEnumerable<string> newMemberChoices;

            do
            {
                newMemberChoices = _ungroupedPlayers.Where(UnderMaxEncounters);
                if (newMemberChoices.Any())                
                    newPlayer = newMemberChoices.ElementAt(_random.Next(newMemberChoices.Count()));                
                else
                    maxEncounters++;
            } while (newPlayer == null);

            if (maxEncounters > MaxEncounters)
                MaxEncounters = maxEncounters;

            return newPlayer;

            bool UnderMaxEncounters(string player)
            {
                foreach (var member in allPreviousPlayers)
                {
                    if (member.ContainsKey(player) && member[player] > maxEncounters)
                        return false;
                }
                return true;
            }
        }

        public int MaxEncounters { get; private set; }

        public int PlayerCount => _players.Count;

        //We want to group the available players into as many 4 player groups as possible, and all remaining groups
        //with 3 players. Given the number of players, GroupCount and NumberOfFourPlayerGroups tell us how to do
        //this. 

        public int GroupCount => ((PlayerCount - 1) / 4) + 1;

        private int NumberOfFourPlayerGroups => PlayerCount - GroupCount * 3;

        /// <summary>
        /// Returns the players that this player has already been grouped with. The keys are the other players, and 
        /// values are the number of times grouped together.
        /// </summary>
        private Dictionary<string, int> PreviousPlayers(string player)
        {
            var previousPlayers = new Dictionary<string, int>();
            foreach (var week in _priorWeeks)
            {
                var playerGroup = week.Groups.FirstOrDefault(g => g.Players.Contains(player));
                if (playerGroup != null)
                {
                    foreach (var otherPlayer in playerGroup.Players.Where(p => p != player))
                    {
                        if (!previousPlayers.ContainsKey(otherPlayer))
                            previousPlayers.Add(otherPlayer, 1);
                        else
                            previousPlayers[otherPlayer]++;
                    }
                }
            }
            return previousPlayers;
        }
    }

    public class Week
    {
        public List<Group> Groups { get; set; } = new List<Group>();

        public class Group
        {
            public List<string> Players { get; set; } = new List<string>();
        }

        public void WriteToFile(string path)
        {
            using (var writer = new StreamWriter(path))
            {
                WriteToTextWriter(writer);
            }
        }

        public void WriteToConsole()
        {
            StringWriter writer = new();
            WriteToTextWriter(writer);
            Console.Write(writer.ToString());
        }


        private void WriteToTextWriter(TextWriter writer)
        {
            foreach (var group in Groups)
            {
                writer.WriteLine("[group]");
                foreach (string player in group.Players)
                    writer.WriteLine(player);
                writer.WriteLine();
            }
        }


        public static Week ReadFromFile(string path)
        {
            List<string> lines = File.ReadAllLines(path).ToList();
            var week = new Week();

            int index = 0;
            List<string> groupPlayers = new();

            while (index < lines.Count)
            {
                string line = lines[index].Trim();
                index++;

                //skip empty or commented out lines
                if (line == string.Empty || line.StartsWith(';'))
                    continue;                

                //start a new group, and put the previous group into an object
                if (line.Equals("[group]", StringComparison.OrdinalIgnoreCase))
                {
                    if (groupPlayers.Any())
                    {
                        week.Groups.Add(new Group() { Players = groupPlayers });
                        groupPlayers = new();
                    }
                }
                else
                {
                    groupPlayers.Add(line);
                }
            }

            if (groupPlayers.Any())
                week.Groups.Add(new Group() { Players = groupPlayers });

            return week;
        }
    }
}
