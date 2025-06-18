//+------------------------------------------------------------------+
//|                                                        FBDv3.mq5 |
//|                                  Copyright 2025, MetaQuotes Ltd. |
//|                                             https://www.mql5.com |
//+------------------------------------------------------------------+
#property copyright "Copyright 2025, MetaQuotes Ltd."
#property link      "https://www.mql5.com"
#property version   "1.00"

#include <Trade\Trade.mqh>
CTrade _cTrade;

// Input parameters
input int      UpdateInterval = 1;      // Update interval in seconds

// Global variables
datetime    lastUpdateTime = 0;
double      lowestPrice; // If this value is set to 0.0, it means the EA was just started
bool        isLowestPriceKnown = false;
double      usedLowestPrice = 0; // Will hold the lowest price which was the basis for tracking sig lows
int         significantLowMarker = 15; // The ride up from a known lowest price that confirms Sig Low
double      inflectionPoint; //This is the price point where the difference was equal to or greater than Sig Low Marker
double      downBreachLowerLimit = 2;
double      downBreachUpperLimit = 13;
int         sequenceSigLow = 1;// Use for ID
bool        existsUsedLowestPrice = false;
double      deflectionPoint;
int         sequenceOrder=1; // For Order Id
int         numberOfAllowedTranches = 2;
int         minLimit = 1;   // For random number generator to generate different order sizes.
int         maxLimit = 100; // same as above


// Struct Lowest Price
struct LowestPrice
   {
      double      dollarValue;
      datetime    foundAt;
   };

LowestPrice _whatIsLowest;

// Struct Buy
struct BuyOrder
  {

   double            boughtAt;
   double            sellAt;
   double            stopLoss;
   double            unitsBought;
   string            state;            //inactive when first created,  used when all portions sold
   int               unitsInHand;
   int               id;
   double            relatedSigLowPrice;
   int               relatedSigLowId;
   datetime          timePlaced;
   string            message;
  };
  
//BuyOrder myBuyOrders[]; // to hold Buy

// Struct: SignificantLow
struct SignificantLow
  {
   double            price;
   int               downBreach; //-1 = flush, 0 = no breach/initialization, 1 = breached
   bool              upBreach;
   datetime          foundAt;
   double            priceFoundAt;
   string            state;
   bool              searchingForUpwardsBreach; // false = initialization, true = counterpart down found; looking for up
   int               sequenceSigLow;
   double            maxBreach;
   datetime          timeMaxBreach;
   BuyOrder          buyOrder;  
   string            message;
   int               hasBuyOrder; // Purpose is to set this to 0 when SL is first created and then when a real Buy is issued, turn this to 1. In checkSellingOpp, I will check if this is 1 or I continue the loop
  };
SignificantLow mySignificantLows[]; // List to hold Significant Lows

//+------------------------------------------------------------------+
//| Expert initialization function                                   |
//+------------------------------------------------------------------+
int OnInit()
  {
   // Optional: Print account info to check if it's a demo account
   if(AccountInfoInteger(ACCOUNT_TRADE_MODE) == ACCOUNT_TRADE_MODE_DEMO)
      Print("Running on a DEMO account.");
   else
      Print("WARNING: Running on a LIVE account!");

   // Display a message when the EA starts
   Print("Real-time data feed started for ", Symbol());

   // Set a timer for regular updates
   EventSetTimer(UpdateInterval);

   // Clear Array of Used Lowest Price
   usedLowestPrice=0;
   sequenceSigLow=0;
   ArrayFree(mySignificantLows);
   srand(GetTickCount());

   // Set isLowestPriceKnown to False
   isLowestPriceKnown = false;
   existsUsedLowestPrice = false;

   Comment("EA Initialized Successfully!");
   Print("Point value for ", Symbol(), " = ", Point());
   Print(OrdersTotal());
   return(INIT_SUCCEEDED);

  }
//+------------------------------------------------------------------+
//| Expert deinitialization function                                 |
//+------------------------------------------------------------------+
void OnDeinit(const int reason)
  {
//--- destroy timer
   EventKillTimer();
   
  }
//+------------------------------------------------------------------+
//| Expert tick function                                             |
//+------------------------------------------------------------------+
void OnTick()
  {
   //---
    // We are focusing in working hours .... between 920 and 4pm ... any ticks before or after is to be ignored...also sig lows have to be deleted
   if(isWithinWorkingHours() == false)
     {
         ArrayFree(mySignificantLows);
         Print("EDT Time: " + GetET(TimeCurrent()) + " -- Outside 10 am and 8 pm in EDT");
         return;
     }
     
     // The price for this tick.
     double latestPrice = SymbolInfoDouble(_Symbol, SYMBOL_BID);
     
     //If we have even one Significant Low known, every tick DEMANDS we check if a Buy is possible
      if(ArraySize(mySignificantLows)>0)
         updateSignificantLows(latestPrice);
         
    // Must check for Selling opportunity
    checkSellingOpportunity(latestPrice);
          
   // #1
   if(isLowestPriceKnown == false)
     {
      
      lowestPrice = latestPrice;
      isLowestPriceKnown = true;
      PrintInfo(latestPrice,lowestPrice, lastUpdateTime," | Lowest price = " + DoubleToString(latestPrice,8) + " at " + TimeToString(GetET(TimeCurrent())));
      PrintSignificantLows();
      return;
     } // #1


   //#2
   if(isLowestPriceKnown == true && existsUsedLowestPrice == false)
     {
      //#3
      if(latestPrice <= lowestPrice)
        {
         lowestPrice = latestPrice;         
         return;
        }//#3

      //#4
      if(latestPrice > lowestPrice)
        {
         double diff = latestPrice - lowestPrice;

         //#5
         if(diff >= significantLowMarker)
           {
            // Create a Significant Low
            datetime _foundAt = GetET(TimeCurrent());
            SignificantLow _sigLow = constructSignificantLow(lowestPrice,latestPrice, _foundAt,"placeholder",0);
            addToSignificantLowList(_sigLow);
            inflectionPoint = latestPrice;  
            existsUsedLowestPrice = true;
            PrintInfo(latestPrice,lowestPrice,lastUpdateTime," | Significant Low set at " + DoubleToString(lowestPrice,8) + " around inflection point " + DoubleToString(inflectionPoint,8));
            PrintSignificantLows();
            return;
           }//#5

         // #6
         else
           {
            PrintInfo(latestPrice,lowestPrice,lastUpdateTime," | Latest price is higher than lowest price by " + DoubleToString(diff,8));
            PrintSignificantLows();
            return;
           }//#6

        }//#4

     }//#2

   //#7
   if(isLowestPriceKnown == true && existsUsedLowestPrice == true)
     {
      //#8
      if(latestPrice >= inflectionPoint) //This will take care of situations where a Sig Low is discovered and the next tick is higher than the inflection point
        {
         inflectionPoint = latestPrice;
         PrintInfo(latestPrice,lowestPrice,lastUpdateTime," | Nothing of interest; Inflection point went higher");
         PrintSignificantLows();
         return;
        }//#8

      if(latestPrice < inflectionPoint) // This condition is when we start to look for secondary sig lows
        {
         existsUsedLowestPrice = false;
         lowestPrice = latestPrice;
         PrintInfo(latestPrice,lowestPrice,lastUpdateTime," | Reset Lowest price to " + DoubleToString(lowestPrice) + " at " + TimeToString(GetET(TimeCurrent())) + "  --- Looking for additional Significant Lows");
         PrintSignificantLows();
         return;
        }
     }//#7
   
  }
  

//+--------------------------------------------------------------------+
// updateSignificantLows(latestPrice)
//+--------------------------------------------------------------------+
void updateSignificantLows(double tmpLatestPrice)
  {

   string tmpMessage;
   
   for(int i=0;i<ArraySize(mySignificantLows);i++)
     {
      SignificantLow tmp = mySignificantLows[i];

      if(StringCompare(tmp.state,"flush",false) == 0 || StringCompare(tmp.state,"used",false) == 0)
        {
         continue;
        }

      // Get Sig Lows Id
      int sigLowId = tmp.sequenceSigLow;

      // Get the lowest price that confirmed this sig low
      double priceThatConfirmedSigLow = tmp.price;

      // Get the flag that confirms/deny downward breach
      // 0=initialization/looking;1=confirmed;-1=flush
      int downWasBreached = tmp.downBreach;

      // Get the flag for searchingUpwards
      bool tmpSearchingUpwardBreach = tmp.searchingForUpwardsBreach;

      // Sig Low was recently confirmed; no breach checks conducted yet; next tick is higher;
      if(priceThatConfirmedSigLow <= tmpLatestPrice  && downWasBreached == 0)
        {
         //This condition doesnt mean anything special for us;
         tmpMessage = " | Nothing special";
         continue;
        }

      // Sig Low was recently confirmed; no downbreach yet; next tick is lower than sig low;
      if(priceThatConfirmedSigLow > tmpLatestPrice  && downWasBreached == 0)
        {

         //Need to check if this breach was in range OR flush.
         if(priceThatConfirmedSigLow-tmpLatestPrice >= 2 && priceThatConfirmedSigLow-tmpLatestPrice <=13)
           {
            //DebugBreak();
            tmp.downBreach = 1;
            tmp.searchingForUpwardsBreach = true;
            deflectionPoint = tmpLatestPrice;
            tmp.maxBreach = setMaxBreach(tmp.maxBreach,tmpLatestPrice);
            tmp.timeMaxBreach = GetET(TimeCurrent());
            
            tmpMessage = " | Significant Low w/ Id " + IntegerToString(sigLowId) + " had a downwards breach; Now looking for upwards motion";
            
            mySignificantLows[i]=tmp;
            continue;
           }//END: Need to check if this breach was in range OR flush.

         //Need to check if this breach was in range OR flush.
         //This will likely NEVER happen because it assumes a crash in price from 100 to 87 or less over one tick
         if(priceThatConfirmedSigLow-tmpLatestPrice > 13)
           {

            //PrintInfo(tmpLatestPrice,lowestPrice,lastUpdateTime," | Significant Low w/ Id " + IntegerToString(sigLowId) + " is flushed");
            tmpMessage = " | Significant Low w/ Id " + IntegerToString(sigLowId) + " is flushed";
            tmp.downBreach = -1;
            tmp.state = "flush";
            mySignificantLows[i]=tmp;
            continue;
           }//END: Need to check if this breach was in range OR flush.

        } //END: Sig Low was recently confirmed; no downbreach yet; next tick is lower than sig low;

      // Sig Low has had a legit down; is it falling to flush? next tick is lower than sig low;
      if(priceThatConfirmedSigLow > tmpLatestPrice && downWasBreached == 1)
        {
        
         tmp.maxBreach = setMaxBreach(tmp.maxBreach,tmpLatestPrice);
         tmp.timeMaxBreach = GetET(TimeCurrent());

         if(priceThatConfirmedSigLow-tmpLatestPrice > 13)
           {
            //PrintInfo(tmpLatestPrice,lowestPrice,lastUpdateTime," | Significant Low w/ Id " + IntegerToString(sigLowId) + " is flushed");
            tmpMessage = " | Significant Low w/ Id " + IntegerToString(sigLowId) + " is flushed";
            tmp.downBreach = -1;
            tmp.state = "flush";
            mySignificantLows[i]=tmp;
            continue;
           }
        }//END: // Sig Low has had a legit down; is it falling to flush? next tick is lower than sig low;

      //Sig Low has had a legit down; its going up!! Buy?
      if(priceThatConfirmedSigLow < tmpLatestPrice && tmpSearchingUpwardBreach)
        {

         if(tmpLatestPrice - priceThatConfirmedSigLow >=2)
           {

            tmp.state = "used";
            tmp.upBreach = true;
            
            int randomOrderSize = GenerateRandomOrderSize(minLimit,maxLimit);
            BuyOrder _buyOrder = createBuyOrder(tmpLatestPrice,tmp.sequenceSigLow,calculatePerUnitProfit(tmp.maxBreach,tmpLatestPrice,2),randomOrderSize,tmp.price,tmp.maxBreach,"active",randomOrderSize + " units @" + tmpLatestPrice);
            _buyOrder.message = _buyOrder.message + "  --- Stop Loss set at " + _buyOrder.stopLoss + "  --- Will sell @: " + _buyOrder.sellAt;
            tmp.buyOrder = _buyOrder;
            tmp.hasBuyOrder = 1;
            //addToBuyList(buyOrder);
           
            tmpMessage = " | Significant Low w/ Id " + IntegerToString(sigLowId) + " was converted to a Buy and will be set to used";
            mySignificantLows[i]=tmp;
            continue;

           }

        }// END: Sig Low has had a legit down; its going up!! Buy?

      if(priceThatConfirmedSigLow > tmpLatestPrice && deflectionPoint < tmpLatestPrice && tmp.searchingForUpwardsBreach)
        {
         // Nothing really happens here .. this is showing we have upward breach happening and we may be able to buy soon
        }

      if(priceThatConfirmedSigLow > tmpLatestPrice && deflectionPoint > tmpLatestPrice && tmp.searchingForUpwardsBreach)
        {
         
         tmp.maxBreach = setMaxBreach(tmp.maxBreach,tmpLatestPrice);
         tmp.timeMaxBreach = GetET(TimeCurrent());
         
         //has the price dropped enough to FLUSH the sig low?
         double diff = priceThatConfirmedSigLow - tmpLatestPrice;

         if(diff > 13)
           {
            tmpMessage = " | Significant Low w/ Id " + IntegerToString(sigLowId) + " is flushed";
            tmp.downBreach = -1;
            tmp.state = "flush";
            mySignificantLows[i]=tmp;
            continue;
           }
        }
        
        PrintInfo(tmpLatestPrice,lowestPrice,lastUpdateTime,tmpMessage);
        PrintSignificantLows();

     }// end of for

  }
 
 //+-------------------------------------------------------------
// Check Selling Opp
//+-------------------------------------------------------------
void checkSellingOpportunity(double _latestPrice)
  {

   for(int i=0; i <ArraySize(mySignificantLows); i++)
     {

         SignificantLow _sl = mySignificantLows[i];

         if(_sl.hasBuyOrder == 0) // Looking for inactive only        
            continue;         
         
         int unitsInHand = _sl.buyOrder.unitsInHand;
         double willSellAt = _sl.buyOrder.sellAt;
         double stopLoss = _sl.buyOrder.stopLoss;
         double unitsBought = _sl.buyOrder.unitsBought;
         string _tmpMessage;
         
         if (_latestPrice >= willSellAt)
         {
            
            //I need to make sure I am breaking the 80% 20% split accurately
            if (unitsInHand == unitsBought)
            {
               //DebugBreak();
               _tmpMessage = " --- Sold " + 0.80*unitsBought + " @ " + _latestPrice;
               _sl.buyOrder.unitsInHand = unitsBought - (0.80*unitsBought);
               _sl.buyOrder.stopLoss = _sl.buyOrder.boughtAt;
               _sl.buyOrder.sellAt = _sl.buyOrder.sellAt + 12;
               _sl.buyOrder.message += _tmpMessage + "\n";
               _sl.buyOrder.message += "                                --- Left: " + _sl.buyOrder.unitsInHand + " units --- Will sell: " + _sl.buyOrder.sellAt + " --- Stop Loss: " + _sl.buyOrder.stopLoss;
               _sl.hasBuyOrder = 1;
               mySignificantLows[i] = _sl; 
               continue;
            }
            
            // This is when 80% is gone
            if (unitsInHand < unitsBought){
               //DebugBreak();
               _tmpMessage = " --- Sold remaining " + unitsInHand + " @" + _sl.buyOrder.sellAt;
               _sl.buyOrder.state = "used";
               _sl.buyOrder.message += _tmpMessage;
               _sl.hasBuyOrder = 0;
               mySignificantLows[i] = _sl; 
               continue;
            }
         }
         
         if (_latestPrice <= stopLoss)
         {
            _tmpMessage = " -- Stop Loss Executed. Sold " + _sl.buyOrder.unitsInHand + " units @ " + stopLoss;
            _sl.buyOrder.state = "used";
            _sl.buyOrder.message += _tmpMessage;
            _sl.hasBuyOrder = 0;
            mySignificantLows[i] = _sl; 
            continue;
         }   
     }
  } 

 //+---------------------------------------------------------
 // Calculate Per Unit Profit
 //+--------------------------------------------------------
 double calculatePerUnitProfit(double _maxBreach,double _latestPrice,double returnMultiplier)
 {
 
   double differenceBtwMaxBreachAndPricePaid = (_latestPrice - _maxBreach)+2;
   
   return differenceBtwMaxBreachAndPricePaid * returnMultiplier;
 }
  
//+---------------------------------------------------------------
// Create Buy Order createBuyOrder(tmpLatestPrice,tmp.sequenceSigLow,10,300,tmp.price);
//+---------------------------------------------------------------
BuyOrder createBuyOrder(double latestPrice,int _relatedSigLowId, double perUnitProfit, int _unitsBought, double _relatedSigLowPrice,double _maxBreach, string _state, string _message)
  {

   BuyOrder tmp;
   

   tmp.boughtAt = latestPrice;
   tmp.relatedSigLowId = _relatedSigLowId;
   tmp.relatedSigLowPrice = _relatedSigLowPrice;
   tmp.id = sequenceOrder;
   sequenceOrder = sequenceOrder + 1;
   tmp.timePlaced = GetET(TimeCurrent());
   tmp.sellAt = latestPrice + perUnitProfit;
   tmp.stopLoss = _maxBreach - 2;
   tmp.unitsBought = _unitsBought;
   tmp.state = _state; //active = just created, used = sold, inactive to show its an empty buy
   tmp.unitsInHand = _unitsBought; // total units in hand... 
   tmp.message  =_message;

   return tmp;

  }
   
//+------------------------------------------------------------------+
//| Custom function to generate a random integer within a range      |
//+------------------------------------------------------------------+
int GenerateRandomOrderSize(int min, int max)
  {
   if (min > max)
     {
      // Swap if min is greater than max to ensure correct range
      int temp = min;
      min = max;
      max = temp;
     }

   // Ensure the range is at least 1 to avoid modulo by zero if min == max
   if (min == max)
     {
      return min; // If limits are the same, just return that limit
     }

   // Calculate the size of the range (inclusive)
   int rangeSize = max - min + 1;

   // Generate a raw random number and scale it to the desired range
   return (min + (MathRand() % rangeSize));  
  }
  
//+------------------------------------------------------------
// Max Breach
//+-----------------------------------------------------------
double setMaxBreach(double currentMaxBreach,double latestPrice)
{

   double tmpMaxBreach = currentMaxBreach;
   
   if (tmpMaxBreach > latestPrice){
      tmpMaxBreach = latestPrice;
   }
   
   return tmpMaxBreach;
}
  
//+------------------------------------------------------------------+
// Construct Significant Low
//+------------------------------------------------------------------+

SignificantLow constructSignificantLow(double tmpLowestPrice, double tmpLatestPrice, datetime _foundAt,string _message,int _hasBuyOrder)
  {

   SignificantLow newSignificantLow;

   newSignificantLow.price = tmpLowestPrice;
   newSignificantLow.downBreach = 0;
   newSignificantLow.foundAt = _foundAt;
   newSignificantLow.searchingForUpwardsBreach = false;
   newSignificantLow.upBreach = false;
   newSignificantLow.state = "active";
   newSignificantLow.sequenceSigLow = sequenceSigLow;
   newSignificantLow.priceFoundAt = tmpLatestPrice;
   newSignificantLow.maxBreach = tmpLowestPrice;
   newSignificantLow.buyOrder = createBuyOrder(0,newSignificantLow.sequenceSigLow,0,0,newSignificantLow.price,0,"inactive", "No Buy Orders");
   sequenceSigLow = sequenceSigLow + 1;
   newSignificantLow.message = _message;
   newSignificantLow.hasBuyOrder = _hasBuyOrder;

   return newSignificantLow;

  }
  
//+------------------------------------------------------------------+
// Add a single Sig Low to the sig low list
//+------------------------------------------------------------------+
void addToSignificantLowList(SignificantLow &significantLow)
  {

// Get current size of array
   int currentSize = ArraySize(mySignificantLows);

// Resize the Array
   ArrayResize(mySignificantLows,currentSize+1);

   mySignificantLows[currentSize] = significantLow;

  }
  

//+--------------------------------------------------------------------
// GetET (takes TimeCurrent and returns a time which is -7 or -8 hours out)
//+-------------------------------------------------------------
datetime GetET(datetime currentServerTime)
  {

      // 7 for EDT, 8 for EST 
      int hours_to_substract = 7;
   
      // Calculate the number of seconds for 7 hours
      int seconds_to_add = hours_to_substract * 3600;
   
      return currentServerTime - seconds_to_add;
  }
  

//+----------------------------------------------------------------------
// Is Within Time
//+----------------------------------------------------------------------
bool isWithinWorkingHours()
  {

      // Get the local server time at MT (its UTC-3)
      datetime _currentServerTime = TimeCurrent();
   
      // Define the number of hours to add -- at time of writing this code, EDT was time zone (UTC-4 or +7 to server time)
      // Will become EST (UTC-5 or plus 8 to server time)
   
      int hours_to_substract = 7;
   
      // Calculate the number of seconds for 7 hours
      int seconds_to_substract  = hours_to_substract * 3600;
   
      // Now get the time it would be in EST/EDT that matches MT time
      datetime market_time = _currentServerTime - seconds_to_substract;
   
   
      bool isWithinTimeRange;
   
      MqlDateTime _mdt;
   
      TimeToStruct(market_time,_mdt);
   
      int futureHour = _mdt.hour;
      int futureMinute = _mdt.min;
   
      // startHour,startMinute,endHour,endMinute is OUR criteria in EDT/EST
      int startHour = 10;
      int startMinute = 00;
      int endHour = 20;
      int endMinute = 0;
   
      bool afterStartTime = (futureHour > startHour) || (futureHour == startHour && futureMinute >= startMinute);
   
      bool beforeEndTime = (futureHour < endHour) ||(futureHour == endHour && futureMinute <= endMinute);
   
      isWithinTimeRange = afterStartTime && beforeEndTime;
   
      return isWithinTimeRange;
  }
  
 //+----------------------------------------------------------------------
 // Print Function
//+---------------------------------------------------------------------
void PrintInfo(double _latestPrice, double _lowestPrice, datetime _timeStamp,string _message)
  {
   Print("********************************************************************");
   Print(GetET(TimeCurrent()),"  Latest Price: ", DoubleToString(_latestPrice,8), "| Lowest Price :", DoubleToString(_lowestPrice,8),_message);
   return;  
  }
  
//+------------------------------------------------------------------------
// Print Significant Lows Only
//+------------------------------------------------------------------------
void PrintSignificantLows()
  {

   for(int i=0; i< ArraySize(mySignificantLows); i++)
     {
      SignificantLow tmp = mySignificantLows[i];
      
      string _howManyBuys = "\n";
      
      if (tmp.hasBuyOrder == 0)
         _howManyBuys = _howManyBuys + "   " +  tmp.buyOrder.message;
      
      if (tmp.hasBuyOrder == 1){
         _howManyBuys = _howManyBuys + "   " +  tmp.buyOrder.message;
      }
      
      Print(
            "\t\tEDT: ", tmp.foundAt,
            " | Sig Low ID: ", tmp.sequenceSigLow,
            " | State: ", tmp.state,
            " | Price: ", tmp.price,
            " | Marker Price: ", tmp.priceFoundAt,
            " | Downbreach: ",tmp.downBreach,
            " | Upward: ", (tmp.upBreach)? " Yes": " No",
            " | Max Breach :", tmp.maxBreach,
            //" | Breached at : ",tmp.timeMaxBreach,
            _howManyBuys
            );
     }

  }
  
//+------------------------------------------------------------------+
//| Timer function                                                   |
//+------------------------------------------------------------------+
void OnTimer()
  {
//---
   
  }
//+------------------------------------------------------------------+
//| Trade function                                                   |
//+------------------------------------------------------------------+
void OnTrade()
  {
//---
   
  }
//+------------------------------------------------------------------+
//| TradeTransaction function                                        |
//+------------------------------------------------------------------+
void OnTradeTransaction(const MqlTradeTransaction& trans,
                        const MqlTradeRequest& request,
                        const MqlTradeResult& result)
  {
//---
   
  }
//+------------------------------------------------------------------+
//| Tester function                                                  |
//+------------------------------------------------------------------+
double OnTester()
  {
//---
   double ret=0.0;
//---

//---
   return(ret);
  }
//+------------------------------------------------------------------+
//| TesterInit function                                              |
//+------------------------------------------------------------------+
void OnTesterInit()
  {
//---
   
  }
//+------------------------------------------------------------------+
//| TesterPass function                                              |
//+------------------------------------------------------------------+
void OnTesterPass()
  {
//---
   
  }
//+------------------------------------------------------------------+
//| TesterDeinit function                                            |
//+------------------------------------------------------------------+
void OnTesterDeinit()
  {
//---
   
  }
//+------------------------------------------------------------------+
//| BookEvent function                                               |
//+------------------------------------------------------------------+
void OnBookEvent(const string &symbol)
  {
//---
   
  }
//+------------------------------------------------------------------+
