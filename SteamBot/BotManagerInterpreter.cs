using System;
using System.IO;
using System.Collections.ObjectModel;
using SteamTrade;
using SteamKit2;
using System.Data.SqlClient;

namespace SteamBot
{
    /// <summary>
    /// A interpreter for the bot manager so a user can control bots.
    /// </summary>
    /// <remarks>
    /// There is currently a small set of commands this can interpret. They 
    /// are limited by the functionality the <see cref="BotManager"/> class
    /// exposes.
    /// </remarks>
    public class BotManagerInterpreter
    {
        private readonly BotManager manager;
        private CommandSet p;
        private int stop = -1;
        private int start = -1;
        private string stopName = String.Empty;
        private bool showHelp;
        private bool clearConsole;

        public BotManagerInterpreter(BotManager manager)
        {
            this.manager = manager;
            p = new CommandSet
                    {
                        new BotManagerOption("stop", "stop (X) where X = index of the configured bot",
                                             s => stop = Convert.ToInt32(s)),
                        new BotManagerOption("start", "start (X) where X = index of the configured bot",
                                             s => start = Convert.ToInt32(s)),
                        new BotManagerOption("stopbot", "stopbot (X) where X = username of the bot", s => stopName = s),
                        new BotManagerOption("help", "shows this help text", s => showHelp = s != null),
                        new BotManagerOption("show",
                                             "show (x) where x is one of the following: index, \"bots\", or empty",
                                             param => ShowCommand(param)),
                        new BotManagerOption("clear", "clears this console", s => clearConsole = s != null),
                        new BotManagerOption("auth", "auth (X)=(Y) where X = index of the configured bot and Y = the steamguard code",
                            AuthSet)
                    };
        }

        void AuthSet(string auth)
        {
            string[] xy = auth.Split('=');

            if (xy.Length == 2)
            {
                int index;

                if (int.TryParse(xy[0], out index))
                {
                    string code = xy[1].Trim();

                    Console.WriteLine("Authing bot with '" + code + "'");
                    manager.AuthBot(index, code);
                }
            }
        }

        /// <summary>
        /// This interprets the given command string.
        /// </summary>
        /// <param name="command">The entire command string.</param>
        public void CommandInterpreter(string command)
        {
            showHelp = false;
            stop = -1;
            start = -1;
            stopName = null;

            p.Parse(command);

            if (showHelp)
            {
                Console.WriteLine("");
                p.WriteOptionDescriptions(Console.Out);
            }

            if (stop > -1)
            {
                manager.StopBot(stop);
            }
            else if (!String.IsNullOrEmpty(stopName))
            {
                manager.StopBot(stopName);
            }

            if (start > -1)
            {
                manager.StartBot(start);
            }

            if (clearConsole)
            {
                clearConsole = false;
                Console.Clear();
            }
        }

        private void ShowCommand(string param)
        {
            param = param.Trim();

            int i;
            if (int.TryParse(param, out i))
            {
                // spit out the bots config at index.
                if (manager.ConfigObject.Bots.Length > i)
                {
                    Console.WriteLine();
                    Console.WriteLine(manager.ConfigObject.Bots[i].ToString());
                }
            }
            else if (!String.IsNullOrEmpty(param))
            {
                if (param.Equals("bots"))
                {
                    // print out the config.Bots array
                    foreach (var b in manager.ConfigObject.Bots)
                    {
                        Console.WriteLine();
                        Console.WriteLine(b.ToString());
                        Console.WriteLine();
                    }
                }
            }
            else
            {
                // print out entire config. 
                // the bots array does not get printed.
                Console.WriteLine();
                Console.WriteLine(manager.ConfigObject.ToString());
            }
        }

        /* given the steamID of a user (as their profile's 64-bit ID number), print the backpack's items*/
        private void getBackpack(string steamID)
        {
            UInt64 nSteamID = Convert.ToUInt64(steamID);
            SteamID id = new SteamID(nSteamID);
            Inventory bp = Inventory.FetchInventory(id.ConvertToUInt64(), "CD7903377EE6EB4862EC01959B039D19");
            if (bp.Items != null)
            {
                foreach (Inventory.Item i in bp.Items)
                {
                    System.Console.WriteLine("-----------");
                    System.Console.WriteLine("Defindex: " + i.Defindex);
                    System.Console.WriteLine("Quality: " + i.Quality);
                    if (i.CustomDescription != null || i.CustomName != null)
                    {
                        System.Console.WriteLine("DIRTY");
                    }
                }
            }
            else
            {
                System.Console.WriteLine("Bad backpack");
            }
        }

        // For each item type in the table, prints the amount in the backpack of [steamID]
        private void statBackpack(string steamID)
        {
            try
            {
                // Get backpack
                UInt64 nSteamID = Convert.ToUInt64(steamID);
                SteamID id = new SteamID(nSteamID);
                Inventory bp = Inventory.FetchInventory(id.ConvertToUInt64(), "CD7903377EE6EB4862EC01959B039D19");
                if (bp.Items == null)
                {
                    Console.WriteLine("bad backpack");
                    return;
                }

                // Read DB
                SqlConnection itemDatabase = new SqlConnection("user id=cdchapma\\Christian;" +
                                           "password=rUbix222;server=CDCHAPMA;" +
                                           "Trusted_Connection=yes;" +
                                           "database=TF2TradeBot; " +
                                           "connection timeout=30");
                itemDatabase.Open();
                SqlCommand getNumberOfTypes = new SqlCommand("SELECT COUNT(\"index\") FROM items", itemDatabase);
                SqlDataReader itemReader = getNumberOfTypes.ExecuteReader();
                itemReader.Read();
                int nOfItemTypes = Convert.ToInt32(itemReader[0].ToString());
                itemReader.Close();
                
                SqlCommand getItems = new SqlCommand("SELECT * FROM items", itemDatabase);
                itemReader = getItems.ExecuteReader();

                // For each DB entry, loop through the backpack looking for clean items of this type
                while (itemReader.Read())
                {
                    int nDefIndex = Convert.ToInt32(itemReader["defindex"].ToString());
                    int nQuality = Convert.ToInt32(itemReader["quality"].ToString());
                    int nOfItems = 0;
                    
                    // Loop through backpack
                    foreach (Inventory.Item i in bp.Items)
                    {
                        if (i.Defindex == nDefIndex)
                        {
                            if (null == i.CustomDescription && null == i.CustomName)
                            {
                                nOfItems++;
                            }
                        }
                    }

                    Console.WriteLine( nOfItems + " items of type \"" + itemReader["name"] + "\" in backpack." );
                }

                itemReader.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        #region Nested Options classes
        // these are very much like the NDesk.Options but without the
        // maturity, features or need for command seprators like "-" or "/"

        private class BotManagerOption
        {
            public string Name { get; set; }
            public string Help { get; set; }
            public Action<string> Func { get; set; }

            public BotManagerOption(string name, string help, Action<string> func)
            {
                Name = name;
                Help = help;
                Func = func;
            }
        }

        private class CommandSet : KeyedCollection<string, BotManagerOption>
        {
            protected override string GetKeyForItem(BotManagerOption item)
            {
                return item.Name;
            }

            public void Parse(string commandLine)
            {
                var c = commandLine.Trim();

                var cs = c.Split(' ');

                foreach (var option in this)
                {
                    if (cs[0].Equals(option.Name, StringComparison.CurrentCultureIgnoreCase))
                    {
                        if (cs.Length > 1)
                        {
                            option.Func(cs[1]);
                        }
                        else
                        {
                            option.Func(String.Empty);
                        }
                    }
                }
            }

            public void WriteOptionDescriptions(TextWriter o)
            {
                foreach (BotManagerOption p in this)
                {
                    o.Write('\t');
                    o.WriteLine(p.Name + '\t' + p.Help);
                }
            }
        }

        #endregion Nested Options classes
    }
}
