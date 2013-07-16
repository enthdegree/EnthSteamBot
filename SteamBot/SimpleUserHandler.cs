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
        List<Inventory.Item> listOfBackpackItems;

        bool bItemAddingMode = false;
        bool bCatalogIsUpToDate = false;
        string szItemCatalog = "";

        public SimpleUserHandler (Bot bot, SteamID sid) : base(bot, sid) 
        {
            botBackpack = new Backpack();
        }

        public override bool OnFriendAdd () 
        {
            return true;
        }

        public override void OnLoginCompleted()
        {
        }

        public override void OnChatRoomMessage(SteamID chatID, SteamID sender, string message)
        {
            Log.Info(Bot.SteamFriends.GetFriendPersonaName(sender) + ": " + message);
            base.OnChatRoomMessage(chatID, sender, message);
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
            valueCustomerOffered = 0;
            valueBotOffered = 0;
            itemsOffered.Clear();

            Bot.GetInventory();
            listOfBackpackItems = new List<Inventory.Item>(Bot.MyInventory.Items); 

            bItemAddingMode = false;
            bCatalogIsUpToDate = false;
            szItemCatalog = "";

            Trade.SendMessage("Welcome to AUTOMATED SHOTGUN TRADER alpha \n" + mainMenu());
        }
        
        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            string s = "You added an item. ";

            int nItemID = botBackpack.getItemID(inventoryItem.Defindex, 
                                                Convert.ToInt32(inventoryItem.Quality));
            
            if ((inventoryItem.CustomName != null || inventoryItem.CustomDescription != null))
            {
                s += "The item you added is not clean. \n This bot cannot currently properly assess the price of unclean items.";
            }
            else if (nItemID != -256)
            {
                double itemValue = botBackpack.computeItemBuyingPrice("76561198070842975", nItemID, listOfBackpackItems);
                if (itemValue < 0)
                {
                    s += "Items of this type are overstocked. \n" +
                         "Value in your current offer: " + (valueCustomerOffered / 9).ToString("N2") + " ref";
                }
                else
                {
                    valueCustomerOffered += itemValue;
                    s += "Value in your current offer: " + (valueCustomerOffered / 9).ToString("N2") + " ref";
                }
            }
            else
            {
                s += "The item you just added is not in our trade database. \n" +
                     "Value in your current offer: " + (valueCustomerOffered / 9).ToString("N2") + " ref";
            }
            
            Trade.SendMessage(s);
        }
        
        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem) 
        {
            int nItemID = botBackpack.getItemID(inventoryItem.Defindex,
                                                Convert.ToInt32(inventoryItem.Quality));
            double nItemValue = botBackpack.computeItemBuyingPrice("76561198070842975", nItemID, listOfBackpackItems);

            // Check to see if the item they removed was one that we wanted.
            // (Otherwise we end up subtracting (-1) from their offer value)
            if(nItemValue >= 0)
            {
                valueCustomerOffered -= nItemValue;
            }

            Trade.SendMessage("You removed an item. \n" +
                              "Value in your current offer: " + (valueCustomerOffered / 9).ToString("N2") + " ref");
        }
        
        public override void OnTradeMessage (string message) {
            if (bItemAddingMode)
            {
                try
                {
                    int nIdToAdd = Convert.ToInt32(message);
                    ItemType itemToAddAttribs = botBackpack.getItemAttribs(nIdToAdd);

                    // If a trade should not be made due to the selling cutoff being exceeded, computeItemSellingPrice() will return -1.
                    // If this happens, just report that the item is out of stock.
                    if (botBackpack.computeItemSellingPrice("76561198070842975", nIdToAdd, listOfBackpackItems) < 0)
                    {
                        Trade.SendMessage(itemCatalog() + "\n--\n Item in too low of stock to sell." );
                    }
                    else
                    {
                        foreach (Inventory.Item i in listOfBackpackItems)
                        {
                            if ( ((i.Defindex == itemToAddAttribs.nDefIndex) || (i.IsNotCraftable && itemToAddAttribs.nDefIndex == 600)) &&
                                 (i.Quality == itemToAddAttribs.nQuality.ToString() ) &&
                                 !(itemsOffered.Contains(i.Id)))
                            {
                                Trade.AddItem(i.Id);
                                itemsOffered.Add(i.Id);
                                valueBotOffered += botBackpack.computeItemSellingPrice("76561198070842975", nIdToAdd, listOfBackpackItems);
                                listOfBackpackItems.Remove(i);
                                Trade.SendMessage("Value bot is offering: " + (valueBotOffered / 9).ToString("N2") + " ref \n--\n" +
                                                    mainMenu());
                                bItemAddingMode = false;
                                return;
                            }
                        }

                        // If we loop through the entire backpack without finding an item of this type, 
                        // report that to the user.
                        Trade.SendMessage(itemCatalog() + "\n--\n Item out of stock.");
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
                        Trade.SendMessage(itemCatalog() + "\n--\n Response could not be parsed.");
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
                        itemsOffered.Clear();
                        listOfBackpackItems = new List<Inventory.Item>(Bot.MyInventory.Items); 
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

            Trade.SendMessage("Value bot is offering: " + (valueBotOffered / 9).ToString("N2") + " ref \n" +
                              "Value you are offering: " + (valueCustomerOffered / 9).ToString("N2"));

            //Because SetReady must use its own version, it's important
            //we poll the trade to make sure everything is up-to-date.
            Trade.Poll();

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
                bool success = false;

                //Even if it is successful, AcceptTrade can fail on
                //trades with a lot of items so we use a try-catch
                try
                {
                    success = Trade.AcceptTrade();
                    Log.Success("Trade Complete!");
                }
                catch (Exception e)
                {
                    Log.Warn("Possible failure at trade accept: " + e.ToString());
                }
                
                bCatalogIsUpToDate = false;
            }

            OnTradeClose();
        }


        public bool Validate()
        {
            if ((valueBotOffered-valueCustomerOffered)/9 > 0.01)
            {
                bItemAddingMode = false;
                Trade.SendMessage("I can't accept this. Please add " + ((valueBotOffered - valueCustomerOffered) / 9).ToString("N2") + " more ref or ask for less items" +
                    "\n--\n" + mainMenu());
                return false;
            }
            else
            {
                if ((valueCustomerOffered - valueBotOffered) / 9 >= 0.3)
                {
                    Trade.SendMessage("!!!WARNING!!! This trade will make you lose more than 1 reclaimed metal in value:\n" 
                        + "The value discrepancy is: " + ((valueCustomerOffered - valueBotOffered) / 9).ToString("N2") + " ref \n" 
                        + "Are you SURE you want to accept this trade?");
                }
                return true;
            }
        }

        public string itemCatalog()
        {
            if (!bCatalogIsUpToDate)
            {
                szItemCatalog = "";
                List<ItemType> itemTypeList = botBackpack.getItemTypeList();
                foreach (ItemType t in itemTypeList)
                {
                    foreach(Inventory.Item i in listOfBackpackItems)
                    {
                        if(( (i.Defindex == t.nDefIndex) || (i.IsNotCraftable && t.nDefIndex == 600) ) &&
                           (i.Quality == t.nQuality.ToString() ))
                        {
                            // Check if we're selling it
                            if (botBackpack.computeItemSellingPrice("76561198070842975", t.nTableIndex, listOfBackpackItems) < 0)
                            {
                                break;
                            }
                            szItemCatalog += "[" + t.nTableIndex + "]: " + t.szName;
                            szItemCatalog += "\n";
                            break;
                        }
                    }
                }
                bCatalogIsUpToDate = true;
            }
            return szItemCatalog;
        }

        public string mainMenu()
        {
            return "Enter a number to make a selection: \n" +
                   "[1]: Add items to buy \n" +
                   "[2]: Clear my offers";
        }

        public override void OnTradeClose()
        {
            valueCustomerOffered = 0;
            valueBotOffered = 0;
            itemsOffered.Clear();
            listOfBackpackItems.Clear();

            bItemAddingMode = false;
            bCatalogIsUpToDate = false;
            szItemCatalog = "";

            Log.Warn("Trade closed.");
            Bot.CloseTrade();
        }

    }

}


