using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamTrade;
using SteamKit2;
using System.Data.SqlClient;

namespace SteamBot
{
    public class ItemType
    {
        public int nTableIndex;
        public int nDefIndex;
        public int nQuality;
        public int nClass;
        public string szName;
        public ItemType(int nTableIndex, int nDefIndex, int nQuality, int nClass, string szName)
        {
            this.nTableIndex = nTableIndex;
            this.nDefIndex = nDefIndex;
            this.nQuality = nQuality;
            this.nClass = nClass;
            this.szName= szName;
        }
    }

    /*
     * This class contains a set of functions to compute buying/selling prices of items (including whether or not to buy or sell the items)
     */
    public class Backpack
    {
        SqlConnection g_itemDatabase;

        public Backpack()
        {
            g_itemDatabase = new SqlConnection("user id=cdchapma\\Christian;" +
                                       "password=rUbix222;server=CDCHAPMA;" +
                                       "Trusted_Connection=yes;" +
                                       "database=TF2TradeBot; " +
                                       "connection timeout=300");
            g_itemDatabase.Open();
        }

        /*
         * Returns a list of all the item types we want to trade
         * 
         * Returns an empty list if something goes wrong.
         */
        public List<ItemType> getItemTypeList()
        {
            List<ItemType> nameList = new List<ItemType>();
            SqlCommand getItems = new SqlCommand("SELECT * FROM items", g_itemDatabase);
            SqlDataReader itemReader = getItems.ExecuteReader();

            try
            {
                while (itemReader.Read())
                {
                    ItemType nameAndIndex = new ItemType(Convert.ToInt32(itemReader["index"]),
                                                         Convert.ToInt32(itemReader["defindex"]),
                                                         Convert.ToInt32(itemReader["quality"]),
                                                         Convert.ToInt32(itemReader["class"]),
                                                         itemReader["name"].ToString());
                    nameList.Add(nameAndIndex);
                }

                itemReader.Close();
                return nameList;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting Item Name List: " + e.ToString());
                nameList.Clear();
                itemReader.Close();
                return nameList;
            }
        }

        /* 
         * Returns a list containing all the database items in the backpack of
         * [steamID]
         * Returns an empty list if something goes wrong.
         * Only includes items that are in the table.
         * 
         */
        public List<Inventory.Item> getBackpack(string steamID)
        {
            List<Inventory.Item> listItems = new List<Inventory.Item>();

            SqlCommand getNumberOfTypes = new SqlCommand("SELECT COUNT(\"index\") FROM items", g_itemDatabase);
            SqlDataReader itemReader = getNumberOfTypes.ExecuteReader();

            SqlDataReader rdrFindItemInTypeTable;

            try
            {
                // Get backpack
                UInt64 nSteamID = Convert.ToUInt64(steamID);
                SteamID id = new SteamID(nSteamID);
                Inventory bp = Inventory.FetchInventory(id.ConvertToUInt64(), "CD7903377EE6EB4862EC01959B039D19");
                if (bp.Items == null)
                {
                    Console.WriteLine("bad backpack");
                    listItems.Clear();
                    return listItems;
                }

                itemReader.Read();
                int nOfItemTypes = Convert.ToInt32(itemReader[0]);
                itemReader.Close();


                foreach (Inventory.Item i in bp.Items)
                {
                        // Check if it's clean and tradable
                    if ((null == i.CustomDescription && null == i.CustomName && !(i.IsNotTradeable))) 
                    {  
                        SqlCommand cmdFindItemInTypeTable;
                        // Quality 600 is used for uncraftable items
                        if( i.IsNotCraftable )
                        {
                            cmdFindItemInTypeTable = new SqlCommand("SELECT * FROM \"items\" WHERE defindex = " + i.Defindex + " AND quality = " + 600, g_itemDatabase );
                        }
                        else
                        {
                            cmdFindItemInTypeTable = new SqlCommand("SELECT * FROM \"items\" WHERE defindex = " + i.Defindex + " AND quality = " + i.Quality, g_itemDatabase );
                        }
                          
                        rdrFindItemInTypeTable = cmdFindItemInTypeTable.ExecuteReader();

                        // Item is in table 
                        if ( rdrFindItemInTypeTable.HasRows )
                        {
                            rdrFindItemInTypeTable.Close();
                            listItems.Add(i);
                        }
                    }
                }

                itemReader.Close();
                return listItems;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                listItems.Clear();
                itemReader.Close();
                return listItems;
            }
        }

        /*
         * Given an item ID, return the item type's class.
         * reutrns -256 if something goes wrong
         */
        public int getItemClass(int itemID)
        {
            SqlCommand getClassRatio = new SqlCommand("SELECT * FROM items WHERE \"index\" =" + itemID, g_itemDatabase);
            SqlDataReader classReader = getClassRatio.ExecuteReader();
            classReader.Read();
            try
            {
                int itemClass = Convert.ToInt32(classReader["class"]);
                classReader.Close();
                return itemClass;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting Item Class Ratio: " + e.ToString());
                classReader.Close();
                return -256;
            }
        }

        
        /*
         * Given an item defindex, quality and series, return the item type's class.
         * reutrns -256 if something goes wrong
         */
        int getItemClass(int nDefIndex, int nQuality)
        {
            return getItemClass(getItemID(nDefIndex, nQuality));
        }

        /*
         * Given a defindex, quality, and series, return the item's ID from the item type database.
         * Return -256 if something goes wrong.
         */
        public int getItemID(int nDefIndex, int nQuality)
        {
            SqlCommand getItemType = new SqlCommand("SELECT *  FROM items WHERE \"defindex\" = " + nDefIndex +
                                                    " AND \"quality\" = " + nQuality,
                                                    g_itemDatabase);
            SqlDataReader itemTypeReader = getItemType.ExecuteReader();
            try
            {
                itemTypeReader.Read();
                int itemIndex = Convert.ToInt32(itemTypeReader["index"]);
                itemTypeReader.Close();
                return itemIndex;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting item type database index: " + e.ToString());
                itemTypeReader.Close();
                return -256;
            }
        }

        /*
         * Given an item type's ID (i.e. `index' column contents), return the
         * item's defIndex and quality.
         * 
         * Returns {nDefIndex, nQuality}.
         * If `nQuality' is -1, all qualities are accepted.
         * 
         * Return {-256,-256,-256,-256,""} if something went wrong
         */
        public ItemType getItemAttribs(int itemID)
        {
            ItemType itemType;
            try
            {
                SqlCommand getItems = new SqlCommand("SELECT *  FROM items WHERE \"index\" = " + itemID, g_itemDatabase);
                SqlDataReader itemReader = getItems.ExecuteReader();
                itemReader.Read();
                itemType = new ItemType(itemID,
                                        Convert.ToInt32(itemReader["defindex"]),
                                        Convert.ToInt32(itemReader["quality"]),
                                        Convert.ToInt32(itemReader["class"]),
                                        Convert.ToString(itemReader["name"]));
                itemReader.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting Item Attributes: " + e.ToString());
                itemType  = new ItemType(-256,-256,-256,-256,"");
            }

            return itemType;
        }

        /*
         * Given some steam ID and an item class (as specified in the database),
         * return the CURRENT ratio of items of [itemClass] to the total number
         * of items in [listItems].
         * 
         * Returns -256 if somethng went wrong.
         */
        public double getCurrentItemClassRatio(string steamID, int itemClass, List<Inventory.Item>listItems)
        {
            int nTotalItems = listItems.Count();
            int nItemsOfThisClass = 0;

            List<ItemType> itemTypesInClass = new List<ItemType>();

            // Get all the types of items of class [itemClass]
            SqlCommand getItemsOfClass = new SqlCommand("SELECT * FROM items WHERE \"class\" = " + itemClass, g_itemDatabase);
            SqlDataReader itemReader = getItemsOfClass.ExecuteReader();

            try
            {
                if (listItems.Count() == 0)
                {
                    // Empty backpack
                    return -256;
                }

                // Put each of the item types into a list
                while (itemReader.Read())
                {
                    ItemType itemType = new ItemType(Convert.ToInt32(itemReader["index"]),
                                                     Convert.ToInt32(itemReader["defindex"]),
                                                     Convert.ToInt32(itemReader["quality"]),
                                                     itemClass,
                                                     itemReader["name"].ToString());
                    itemTypesInClass.Add(itemType);
                }

                itemReader.Close();

                // Check each item in the backpack to see if it matches an item
                // type in the list, and add 1 to our cumulative total for each
                // one that does.
                foreach (Inventory.Item i in listItems)
                {
                    foreach (ItemType j in itemTypesInClass)
                    {
                        if ((i.Defindex == j.nDefIndex || (j.nDefIndex == 600 && i.IsNotCraftable)) &&
                            i.Quality == j.nQuality.ToString() )
                        {
                            nItemsOfThisClass++;
                        }
                    }
                }

                return (double)nItemsOfThisClass / nTotalItems;
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Error in calculating getItemClassRatio(): " + e.ToString());
                itemReader.Close();
                return -256;
            }
        }

        /*
         * Given some item class (as specified in the database), return the
         * DESIRED ratio of items of [itemClass] to the total number of backpack 
         * items.
         * 
         * Only items of types in the table are counted
         * 
         * Returns -256 if something went wrong.
         */
        public double getDesiredItemClassRatio(int itemClass)
        {
            SqlCommand getClassRatio = new SqlCommand("SELECT * FROM classes WHERE \"class\" =" + itemClass, g_itemDatabase);
            SqlDataReader classReader = getClassRatio.ExecuteReader();
            try
            {
                classReader.Read();
                double dRatio = Convert.ToDouble(classReader["ratio"]);
                classReader.Close();
                return dRatio;
            }
            catch (Exception e)
            {
                classReader.Close();
                Console.WriteLine("Error getting Item Class Ratio: " + e.ToString());
                return -256.0;
            }
        }

        /*
         * Given a steamID and itemID, returns the price in scrap metal to sell an item of type [itemID] at.
         * Return -1.0 if a trade should not be made.
         * Return -256.0 if something went wrong
         */
        public double computeItemSellingPrice(string steamID, int itemID, List<Inventory.Item> listItems)
        {

            double nPrice = 1.0;

            // Item index of the currency [nPrice] is in terms of.
            int nInTermsOf = itemID;

            // Now convert [nPrice] to be in terms of Scrap Metal.
            while (nInTermsOf != 0)
            {
                SqlCommand getItemType = new SqlCommand("SELECT * FROM items WHERE \"index\" = " + nInTermsOf, g_itemDatabase);
                SqlDataReader itemTypeReader = getItemType.ExecuteReader();
                try
                {
                    itemTypeReader.Read();
                    int nItemClass = Convert.ToInt32(itemTypeReader["class"]);
                    double dMinPrice = Convert.ToDouble(itemTypeReader["minPrice"]);
                    double dMaxPrice = Convert.ToDouble(itemTypeReader["maxPrice"]);
                    double dMarkup = Convert.ToDouble(itemTypeReader["sellingMarkup"]);
                    nInTermsOf = Convert.ToInt32(itemTypeReader["inTermsOf"]);
                    itemTypeReader.Close();

                    // Compute the value of the currency [nPrice] is in terms of.
                    // (using the formula found at http://nerdhow.com/tf2-automated-trading/ )
                    double currencyValue = ((dMinPrice + dMaxPrice) / 2.0 + (dMaxPrice - dMinPrice) / 2.0 *
                                            (getDesiredItemClassRatio(nItemClass) - getCurrentItemClassRatio(steamID, nItemClass, listItems)) / getDesiredItemClassRatio(nItemClass));

                    if (currencyValue > dMaxPrice)
                    {
                        currencyValue = dMaxPrice;
                    }
                    if (currencyValue < (dMinPrice + dMaxPrice)/2 + dMarkup)
                    {
                        currencyValue = (dMinPrice + dMaxPrice)/2 + dMarkup;
                    }

                    // Multiply our price by that value to convert nPrice to now be in terms of another currency.
                    nPrice = nPrice * currencyValue;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error computing selling price: " + e.ToString());
                    itemTypeReader.Close();
                    return -256.0;
                }
                // Repeat until we have [nPrice] in terms of scrap metal (which has index 1)
            }

            int itemClass = getItemClass(itemID);
            SqlCommand getClass = new SqlCommand("SELECT * FROM classes WHERE \"class\" = " + itemClass, g_itemDatabase);
            SqlDataReader classReader = getClass.ExecuteReader();
            try
            {
                classReader.Read();
                double dSellingCutoff = Convert.ToDouble(classReader["sellingCutoff"]);
                classReader.Close();

                double d = (getDesiredItemClassRatio(itemClass) - getCurrentItemClassRatio(steamID, itemClass,listItems)) / getDesiredItemClassRatio(itemClass);
                if ((getDesiredItemClassRatio(itemClass) - getCurrentItemClassRatio(steamID, itemClass,listItems)) / getDesiredItemClassRatio(itemClass)
                        > dSellingCutoff)
                {
                    return -1.0;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error computing selling price: " + e.ToString());
                classReader.Close();
                return -256.0;
            }

            return nPrice;
        }

        /*
         * Given a steamID and itemID, returns the price in scrap metal to buy an item of type [itemID] for.
         * Return -1.0 if a trade should not be made.
         * Return -256.0 if something went wrong
         */
        public double computeItemBuyingPrice(string steamID, int itemID, List<Inventory.Item> listItems)
        {
                double nPrice = 1.0;

                // Item index of the currency [nPrice] is in terms of.
                int nInTermsOf = itemID;

                // Now convert [nPrice] to be in terms of Scrap Metal.
                while (nInTermsOf != 0)
                {
                    SqlCommand getItemType = new SqlCommand("SELECT * FROM items WHERE \"index\" = " + nInTermsOf, g_itemDatabase);
                    SqlDataReader itemTypeReader = getItemType.ExecuteReader();
                    try
                    {
                        itemTypeReader.Read();
                        int nItemClass = Convert.ToInt32(itemTypeReader["class"]);
                        double dMinPrice = Convert.ToDouble(itemTypeReader["minPrice"]);
                        double dMaxPrice = Convert.ToDouble(itemTypeReader["maxPrice"]);
                        double dMarkdown = Convert.ToDouble(itemTypeReader["buyingMarkdown"]);
                        nInTermsOf = Convert.ToInt32(itemTypeReader["inTermsOf"]);
                        itemTypeReader.Close();

                        // Compute the value of the currency [nPrice] is in terms of.
                        // (using the formula found at http://nerdhow.com/tf2-automated-trading/ )
                        double tendencyScalar = (getDesiredItemClassRatio(nItemClass) - getCurrentItemClassRatio(steamID, nItemClass, listItems)) / getDesiredItemClassRatio(nItemClass);
                        double currencyValue = ((dMinPrice + dMaxPrice) / 2.0 + (dMaxPrice - dMinPrice) / 2.0 * tendencyScalar);

                        if (currencyValue < dMinPrice)
                        {
                            currencyValue = dMinPrice;
                        }
                        if (currencyValue > (dMinPrice + dMaxPrice) / 2 - dMarkdown)
                        {
                            currencyValue = (dMinPrice + dMaxPrice) / 2 - dMarkdown;
                        }

                        // Multiply our price by that value to convert nPrice to now be in terms of another currency.
                        nPrice = nPrice * currencyValue;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error computing buying price: " + e.ToString());
                        itemTypeReader.Close();
                        return -256.0;
                    }

                    // Repeat until we have [nPrice] in terms of scrap metal (which has index 1)
                }

                int itemClass = getItemClass(itemID);
                SqlCommand getClass = new SqlCommand("SELECT * FROM classes WHERE \"class\" = " + itemClass, g_itemDatabase);
                SqlDataReader classReader = getClass.ExecuteReader();
                try
                {
                    classReader.Read();
                    double dBuyingCutoff = Convert.ToDouble(classReader["buyingCutoff"]);
                    classReader.Close();
                    double tendencyScalar = (getDesiredItemClassRatio(itemClass) - getCurrentItemClassRatio(steamID, itemClass, listItems)) / getDesiredItemClassRatio(itemClass);
                    if ( tendencyScalar < dBuyingCutoff)
                    {
                        return -1.0;
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine("Error computing buying price: " + e.ToString());
                    classReader.Close();
                    return -256.0;
                }
                return nPrice;
        }
    }
}
