using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using SteamBot;
using System;

namespace SteamBot
{
    public class SimpleUserHandler : UserHandler
    {
        public double valueCustomerOffered = 0;
        public double valueBotOffered = 0;
        public List<ulong> itemsOffered = new List<ulong>();
        Backpack botBackpack;
        bool bItemAddingMode = false;

        public SimpleUserHandler (Bot bot, SteamID sid) : base(bot, sid) 
        {
            botBackpack = new Backpack();
        }

        public override bool OnFriendAdd () 
        {
            return true;
        }
        
        public override void OnFriendRemove () {}
        
        public override void OnMessage (string message, EChatEntryType type)
        {
            Bot.SteamFriends.SendChatMessage(OtherSID, type, Bot.ChatResponse);
            Bot.SteamFriends.SendChatMessage(OtherSID, type, "Send a trade request and this bot will accept if it is not busy with another user!");
        }

        public override bool OnTradeRequest() 
        {
            return true;
        }
        
        public override void OnTradeError (string error) 
        {
            Bot.SteamFriends.SendChatMessage (OtherSID, 
                                              EChatEntryType.ChatMsg,
                                              "Error: " + error + "." );
            Bot.log.Warn (error);
        }
        
        public override void OnTradeTimeout () 
        {
            Bot.SteamFriends.SendChatMessage (OtherSID, EChatEntryType.ChatMsg,
                                              "User timed out.");
            Bot.log.Info ("User kicked due to timeout.");
        }
        
        public override void OnTradeInit()
        {
            Trade.SendMessage("Welcome to AUTOMATED SHOTGUN TRADER alpha \n" + mainMenu());
        }
        
        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem) 
        {
            int nItemID = botBackpack.getItemID( schemaItem.Defindex, schemaItem.ItemQuality );

            string s = "You added an item. ";

            if (nItemID != -256)
            {
                double itemValue = botBackpack.computeItemBuyingPrice("76561198070842975", nItemID);
                if (itemValue < 0)
                {
                    s += "Items of this type are overstocked. \n" +
                         "Value in your current offer: " + valueCustomerOffered/9 + " ref";
                }
                else
                {
                    valueCustomerOffered += itemValue;
                    s += "Value in your current offer: " + valueCustomerOffered / 9 + " ref";
                }
            }
            else
            {
                s += "The item you just added is not in our trade database. \n" +
                     "Value in your current offer: " + valueCustomerOffered/9 + " ref";
            }
            
                Trade.SendMessage(s);
        }
        
        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) 
        {
            int nItemID = botBackpack.getItemID(schemaItem.Defindex, schemaItem.ItemQuality);
            double nItemValue = botBackpack.computeItemBuyingPrice("76561198070842975", nItemID);

            // Check to see if the item they removed was overstocked. 
            // We don't want to subtract (-1) from their offer's value for their removing an item.
            if( nItemValue >= 0)
            {
                valueCustomerOffered -= nItemValue;
            }

            Trade.SendMessage("You removed an item. \n" +
                              "Value in your current offer: " + valueCustomerOffered/9 + " ref");
        }
        
        public override void OnTradeMessage (string message) {
            if (bItemAddingMode)
            {
                try
                {
                    int nIdToAdd = Convert.ToInt32(message);
                    int[] itemToAddAttribs = botBackpack.getItemAttribs(nIdToAdd);

                    // If a trade should not be made due to the selling cutoff being exceeded, computeItemSellingPrice() will return -1.
                    // If this happens, just report that the item is out of stock.
                    if (botBackpack.computeItemSellingPrice("76561198070842975", nIdToAdd) < 0)
                    {
                        Trade.SendMessage("Item out of stock. \n--\n" + itemCatalog());
                    }
                    else
                    {
                        List<Inventory.Item> itemsInBackpack = botBackpack.getBackpack("76561198070842975");
                        foreach (Inventory.Item i in itemsInBackpack)
                        {
                            if (i.Defindex == itemToAddAttribs[0])
                            {
                                if ( (i.Quality == itemToAddAttribs[1].ToString() || itemToAddAttribs[1] == -1)
                                        && !(itemsOffered.Contains(i.Id)) )
                                {
                                    Trade.AddItem(i.Id);
                                    itemsOffered.Add(i.Id);
                                    valueBotOffered += botBackpack.computeItemSellingPrice("76561198070842975", nIdToAdd);
                                    Trade.SendMessage("Value bot is offering: " + valueBotOffered/9 + " ref \n--\n" +
                                                       mainMenu());
                                    bItemAddingMode = false;
                                    return;
                                }
                            }
                        }

                        // If we loop through the entire backpack without finding an item of this type, 
                        // report that to the user.
                        Trade.SendMessage("Item out of stock. \n--\n" + itemCatalog());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());

                    if ("X" == message.ToUpper())
                    {
                        bItemAddingMode = false;
                        Trade.SendMessage(mainMenu());
                    }
                    else
                    {
                        Trade.SendMessage("Response could not be parsed.\n--\n" + itemCatalog() );
                    }
                }
            }

            // Main menu
            else
            {
                switch (message)
                {
                    case "1":
                        bItemAddingMode = true;
                        Trade.SendMessage(itemCatalog() +
                                          "\n [X]: Cancel");
                        break;

                    case "2":
                        foreach (ulong id in itemsOffered)
                        {
                            Trade.RemoveItem(id);
                        }
                        valueBotOffered = 0;
                        Trade.SendMessage(mainMenu());
                        break;

                    default:
                        bItemAddingMode = false;
                        Trade.SendMessage("Response could not be parsed. \n--\n" + mainMenu());
                        break;
                }
            }
        }
        
        public override void OnTradeReady(bool ready) 
        {
            Trade.SendMessage("Value bot is offering: " + valueBotOffered/9 + " ref \n" +
                              "Value you are offering: " + valueCustomerOffered/9 );
            if (!ready)
            {
                Trade.SetReady(false);
                Trade.SendMessage(mainMenu());
            }
            else
            {
                if(Validate())
                {
                    Trade.SetReady(true);
                }
            }
        }
        
        public override void OnTradeAccept() 
        {
            if (Validate() || IsAdmin)
            {
                bool success = Trade.AcceptTrade();

                if (success)
                {
                    Log.Success("Trade successful!");
                }
                else
                {
                    Log.Warn("Trade may have failed.");
                }
            }

            OnTradeClose();
        }

        public bool Validate()
        {
            if (valueCustomerOffered < valueBotOffered)
            {
                bItemAddingMode = false;
                Trade.SendMessage("I can't accept this. Please add " + (valueBotOffered - valueCustomerOffered) / 9 + " more ref or ask for less items" +
                    "\n--\n" + mainMenu());
                return false;
            }
            else
            {
                return true;
            }
        }

        public string itemCatalog()
        {
            string s = "";
            List<string[]> nameList = botBackpack.getItemNameList();
            foreach (string[] nameAndIndex in nameList)
            {
                s += "[" + nameAndIndex[0] + "]: " + nameAndIndex[1];
                s += "\n";
            }
            return s;
        }

        public string mainMenu()
        {
            return "Enter a number to make a selection: \n" +
                   "[1]: Add items to buy \n" +
                   "[2]: Clear my offers";
        }
    }
 
}

