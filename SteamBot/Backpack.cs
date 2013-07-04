using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SteamTrade;
using SteamKit2;
using System.Data.SqlClient;

namespace SteamBot
{
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
         * Returns a list of string arrays of 2 elements, each containing:
         * [0]: The item type's ID in the `items' table (i.e. the contents of the item's `index' entry)
         * [1]: A descriptive, human-readable name (e.g. "Strange Festive Huntsman")
         * 
         * Returns an empty list if something goes wrong.
         */
        public List<string[]> getItemNameList()
        {
            List<string[]> nameList = new List<string[]>();
            SqlCommand getItems = new SqlCommand("SELECT * FROM items", g_itemDatabase);
            SqlDataReader itemReader = getItems.ExecuteReader();

            try
            {
                while (itemReader.Read())
                {
                    string[] nameAndIndex = new string[2];
                    nameAndIndex[0] = itemReader["index"].ToString();
                    nameAndIndex[1] = itemReader["name"].ToString();
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
                int nOfItemTypes = Convert.ToInt32(itemReader[0].ToString());
                itemReader.Close();

                SqlCommand getItems = new SqlCommand("SELECT * FROM items", g_itemDatabase);
                itemReader = getItems.ExecuteReader();

                // For each DB entry, loop through the backpack looking for clean items of this type
                while (itemReader.Read())
                {
                    int nDefIndex = Convert.ToInt32(itemReader["defindex"].ToString());
                    int nQuality = Convert.ToInt32(itemReader["quality"].ToString());

                    // Loop through backpack
                    foreach (Inventory.Item i in bp.Items)
                    {
                        if (i.Defindex == nDefIndex)
                        {
                            if (i.Quality == nQuality.ToString() || nQuality == -1)
                            {
                                // Finally, check if it's clean.
                                if (null == i.CustomDescription && null == i.CustomName)
                                {
                                    listItems.Add(i);
                                }
                            }
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
            try
            {
                SqlCommand getClassRatio = new SqlCommand("SELECT * FROM items WHERE \"index\" =" + itemID, g_itemDatabase);
                SqlDataReader classReader = getClassRatio.ExecuteReader();
                classReader.Read();
                int itemClass = Convert.ToInt32(classReader["class"]);
                classReader.Close();
                return itemClass;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting Item Class Ratio: " + e.ToString());
                return -256;
            }
        }

        /*
         * Given a Defindex and a quality, return the item's ID from the item type database.
         * Return -256 if something goes wrong.
         */
        public int getItemID(int nDefIndex, int nQuality)
        {
            SqlCommand getItemType = new SqlCommand("SELECT *  FROM items WHERE \"defindex\" = " + nDefIndex +
                                                      " AND quality = " + nQuality, g_itemDatabase);
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
         * Return {-256,-256} if something went wrong
         */
        public int[] getItemAttribs(int itemID)
        {
            int[] itemAttribs = new int[2];
            try
            {
                SqlCommand getItems = new SqlCommand("SELECT *  FROM items WHERE \"index\" = " + itemID, g_itemDatabase);
                SqlDataReader itemReader = getItems.ExecuteReader();
                itemReader.Read();
                itemAttribs[0] = Convert.ToInt32(itemReader["defindex"]);
                itemAttribs[1] = Convert.ToInt32(itemReader["quality"]);
                itemReader.Close();
                return itemAttribs;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error getting Item Attributes: " + e.ToString());
                itemAttribs[0] = -256;
                itemAttribs[1] = -256;
                return itemAttribs;
            }
        }

        /*
         * Given some steam ID and an item class (as specified in the database),
         * return the CURRENT ratio of items of [itemClass] to the total number
         * of backpack items.
         * 
         * Only items of types defined in the table are counted
         * 
         * Returns -256 if somethng went wrong.
         */
        public double getCurrentItemClassRatio(string steamID, int itemClass)
        {
            List<Inventory.Item> listItems = getBackpack(steamID);

            // Get all the types of items of class [itemClass]
            SqlCommand getItemsOfClass = new SqlCommand("SELECT * FROM items WHERE \"class\" = " + itemClass, g_itemDatabase);
            SqlDataReader itemReader = getItemsOfClass.ExecuteReader();

            try
            {
                if (listItems.Count() == 0)
                {
                    // Empty backpack
                    return -1;
                }

                int nTotalItems = listItems.Count();
                int nClassItems = 0;

                // For each item type, add how many of it are in [listItems] to our running total, [nClassItems]
                while (itemReader.Read())
                {
                    int nDefIndex = Convert.ToInt32(itemReader["defindex"].ToString());
                    int nQuality = Convert.ToInt32(itemReader["quality"].ToString());

                    foreach (Inventory.Item i in listItems)
                    {
                        if (i.Defindex == itemClass)
                        {
                            if (Convert.ToInt32(i.Quality) == nQuality)
                            {
                                nClassItems++;
                            }
                        }
                    }
                }
                itemReader.Close();
                return (double)nClassItems / nTotalItems;
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
        public double computeItemSellingPrice(string steamID, int itemID)
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
                                            (getDesiredItemClassRatio(nItemClass) - getCurrentItemClassRatio(steamID, nItemClass)) / getDesiredItemClassRatio(nItemClass));

                    if (currencyValue > dMaxPrice)
                    {
                        currencyValue = dMaxPrice;
                    }
                    if (currencyValue < dMinPrice + dMarkup)
                    {
                        currencyValue = dMinPrice + dMarkup;
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

                double d = (getDesiredItemClassRatio(itemClass) - getCurrentItemClassRatio(steamID, itemClass)) / getDesiredItemClassRatio(itemClass);
                if ((getDesiredItemClassRatio(itemClass) - getCurrentItemClassRatio(steamID, itemClass)) / getDesiredItemClassRatio(itemClass)
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
        public double computeItemBuyingPrice(string steamID, int itemID)
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
                        double currencyValue = ((dMinPrice + dMaxPrice) / 2.0 + (dMaxPrice - dMinPrice) / 2.0 *
                                               (getDesiredItemClassRatio(nItemClass) - getCurrentItemClassRatio(steamID, nItemClass)) / getDesiredItemClassRatio(nItemClass));

                        if (currencyValue < dMinPrice)
                        {
                            currencyValue = dMinPrice;
                        }
                        if (currencyValue < dMaxPrice - dMarkdown)
                        {
                            currencyValue = dMinPrice - dMarkdown;
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

                if ((getDesiredItemClassRatio(itemClass) - getCurrentItemClassRatio(steamID, itemClass)) / getDesiredItemClassRatio(itemClass)
                      < dBuyingCutoff)
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
