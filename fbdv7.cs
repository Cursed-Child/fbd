using System;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System.Collections.Generic;
using System.IO;

namespace cAlgo.Robots;


//********************************* *******//
// 30 % : Selling at PRICE PAID + GAP; Stop Loss at Sig Low - 2
// 40 %: Selling at PRICE PAID + (2 * GAP); Stop Loss is at PRICE PAID
// 30 %  Selling at PRICE PAID + (3 * GAP); Stop Loss is at PRICE PAID + GAP (OTO)
//********************************


#region Low Price 
public class LowPrice
{
    public double Price { get; set; }
    public DateTime TimeFound { get; set; }
    public bool ConvertedToSigLow { get; set; }
    public DateTime ExpiryTime { get; set; }
    public string Log { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string Status { get; set; } // active, converted, expired

    // Lows will have to expire eventually either because its EOD or they hit their ExpiryTime
    // EOD = 1, Decay (due to Expiry Time being reached) = 2
    public int ExpirationStrategy { get; set; }
    public long Id { get; set; }

    // Associated Significant Low
    public SignificantLow AssociatedSignificantLow { get; set; }

    // Added 'int expiryMinutes' to the constructor parameters
    public LowPrice(double _price, DateTime _timeFound, int _expirationStrategy, int _expiryMinutes, long _id, int _digits, int _expiryHour, int _expiryMinute, int _expirySecond)
    {
        Price = _price;
        TimeFound = _timeFound;
        ConvertedToSigLow = false;
        //ExpiryTime = _timeFound.AddMinutes(_expiryMinutes); // This value will ONLY be used for zombie-ing a low price object if _expirationStrategy is NOT 1
        Status = "ACTIVE";
        Id = _id;
        ExpirationStrategy = _expirationStrategy;

        // Added for logging 
        string _formattedPrice = _price.ToString("F" + _digits);

        // Setting Price to _formattedPrice
        Price = double.Parse(_formattedPrice);

        if (_expirationStrategy == 1)
        {
            ExpiryTime = new DateTime(_timeFound.Year, _timeFound.Month, _timeFound.Day, _expiryHour, _expiryMinute, _expirySecond);
            //Log = Environment.NewLine + $"Low Price # {_id} | Price: {_formattedPrice} | Found: {_timeFound} | Status: {Status} | Expires: {ExpiryTime}";
        }
        else
        {
            ExpiryTime = _timeFound.AddMinutes(_expiryMinutes);
            //Log = Environment.NewLine + $"Low Price # {_id} | Price: {_formattedPrice} | Found: {_timeFound} | Status: {Status} | Expires: {ExpiryTime}";
        }
    }
}
#endregion

#region Significant Low
public class SignificantLow
{

    public long Id { get; set; }
    public double Price { get; set; }
    public DateTime TimeFound { get; set; }
    public bool IsActive { get; set; }
    public double MarkerPrice { get; set; }
    public string Log { get; set; } = string.Empty;
    public double Ceiling { get; set; }
    public double Floor { get; set; }
    public double MaxBreach { get; set; }
    public string State { get; set; }

    //** down breach
    public bool HasBreachedDown { get; set; }
    public DateTime DownBreachTime { get; set; }
    public double DownBreachTickerPrice { get; set; }

    //** up breach
    public bool HasBreachedUp { get; set; }
    public DateTime UpBreachTime { get; set; }
    public double UpBreachTickerPrice { get; set; }

    // ** 
    public double BuyAt { get; set; }
    public DateTime ConvertedToBuyTime { get; set; }
    public double ConvertedToBuyTickerPrice { get; set; }

    // ** Expiry
    public int ExpirationStrategy { get; set; }
    public DateTime ExpirationTime { get; set; }
    public double Ride { get; set; }
    public BuyOrder BuyOrder { get; set; }

    public SignificantLow(long _id, double _price, DateTime _timeFound, double _markerPrice, int _significantLowExpirationStrategy, int _significantLowExpirationMinute, int _expiryHour, int _expiryMinute, int _expirySecond)
    {
        Id = _id;
        Price = _price;
        TimeFound = _timeFound;
        IsActive = true;
        MarkerPrice = _markerPrice;
        Ceiling = _price - 2;
        Floor = _price - 5;
        MaxBreach = _price;
        HasBreachedDown = false;
        HasBreachedUp = false;
        BuyAt = _price + 2;
        Ride = Math.Round(_markerPrice - _price, 2);
        ExpirationStrategy = _significantLowExpirationStrategy;

        if (_significantLowExpirationStrategy == 1)
        {
            ExpirationTime = new DateTime(_timeFound.Year, _timeFound.Month, _timeFound.Day, _expiryHour, _expiryMinute, _expirySecond);
            //Log = Environment.NewLine + $"Low Price # {_id} | Price: {_formattedPrice} | Found: {_timeFound} | Status: {Status} | Expires: {ExpiryTime}";
        }
        else
        {
            ExpirationTime = _timeFound.AddMinutes(_significantLowExpirationMinute);
            //Log = Environment.NewLine + $"Low Price # {_id} | Price: {_formattedPrice} | Found: {_timeFound} | Status: {Status} | Expires: {ExpiryTime}";
        }

        // Internal variable for logging
        double _ride = _markerPrice - _price;
        _ride = Math.Round(_ride, 4);

        State = "ACTIVE";
    }
}
#endregion

#region Buy
public class BuyOrder
{

    private readonly Robot _robot;

    public long Id { get; set; }
    public DateTime DateBought { get; set; }
    public double PricePaid { get; set; }
    public double OTO { get; set; }
    public double Gap { get; set; }
    public double UnitsBought { get; set; }
    public double UnitsInHand { get; set; }
    public Dictionary<int, SubOrder> SubOrders { get; set; }
    public int ActiveSubOrder { get; set; }
    public string State { get; set; }
    public string Log { get; set; }

    public double TotalProfit
    {

        get
        {
            double total = 0;
            if (SubOrders != null)
            {
                foreach (var sub in SubOrders.Values)
                    total += sub.Profit;
            }

            return Math.Round(total, 2);
        }
    }

    public BuyOrder(Robot _bot, SignificantLow _sigLowObject, DateTime _dateBought, double _pricePaid, double _gap, double _unitsBought)
    {
        try
        {

            _robot = _bot;
            Id = _sigLowObject.Id;
            DateBought = _dateBought;
            PricePaid = _pricePaid;
            Gap = _gap;
            OTO = _pricePaid + _gap;
            UnitsBought = _unitsBought;
            UnitsInHand = _unitsBought;
            State = "ACTIVE";
            ActiveSubOrder = 1;
            SubOrders = createSubOrders(_robot, _sigLowObject.Id, _pricePaid, _gap, _unitsBought, _sigLowObject.Price);

            if (SubOrders is null) throw new Exception($"Sub Orders for buy # {_sigLowObject.Id} are null");
        }
        catch (Exception ex)
        {
            _bot.Print($"Error in BuyOrder class for buy # {_sigLowObject.Id}");
        }
    }

    #region Create Sub Orders
    protected Dictionary<int, SubOrder> createSubOrders(Robot _bot, long _id, double _pricePaid, double _gap, double _unitsBought, double _sigLowPrice)
    {

        double[] _ratio = { 0.30, 0.40, 0.30 };
        Dictionary<int, SubOrder> _listOfSubOrders = new Dictionary<int, SubOrder>();
        double _totalUnitsAllocatedSoFar = 0;

        try
        {

            for (int i = 1; i <= _ratio.Length; i++)
            {

                double normalizedUnits;
                double sellAt = 0;
                double stopLoss = 0;

                // 1. Calculate Volume (with remainder check on the last order)
                if (i == _ratio.Length)
                {
                    normalizedUnits = _unitsBought - _totalUnitsAllocatedSoFar;
                }
                else
                {
                    double rawUnits = _unitsBought * _ratio[i - 1];
                    normalizedUnits = _robot.Symbol.NormalizeVolumeInUnits(rawUnits, RoundingMode.ToNearest);
                }

                // 2. Apply your specific Price Rules
                if (i == 1)
                {
                    sellAt = _pricePaid + _gap;
                    stopLoss = _sigLowPrice - 2; // Price Paid - 4;
                }
                else if (i == 2)
                {
                    sellAt = _pricePaid + (2 * _gap);
                    stopLoss = _pricePaid;
                }
                else if (i == 3)
                {
                    sellAt = _pricePaid + (3 * _gap);
                    // Stop Loss is set to the SellAt of the previous order (i=2)
                    stopLoss = _pricePaid + _gap;
                }

                // 3. Precision Rounding for Prices (prevents broker errors)
                sellAt = Math.Round(sellAt, _robot.Symbol.Digits);
                stopLoss = Math.Round(stopLoss, _robot.Symbol.Digits);

                _listOfSubOrders.Add(i, new SubOrder(_id, normalizedUnits, sellAt, stopLoss, DateTime.MinValue, i));

                _totalUnitsAllocatedSoFar += normalizedUnits;
            }

            return _listOfSubOrders;
        }

        catch (Exception ex)
        {
            _bot.Print($"Error while creating sub orders for buy # {_id}. Reason: {ex.Message}");
            return null;
        }
    }
    #endregion
}
#endregion

#region Sub Order 
public class SubOrder
{

    public double UnitsAllocated { get; set; }
    public double SellAt { get; set; }
    public double StopLoss { get; set; }
    public DateTime WhenSold { get; set; }
    public long Id { get; set; }
    public int SubOrderSequence { get; set; }
    public double Profit { get; set; }
    public string Log { get; set; }
    public double ExecutedPrice { get; set; }

    public SubOrder(long _id, double _unitsAllocated, double _sellAt, double _stopLoss, DateTime _whenSold, int _subOrderSequence)
    {

        Id = _id;
        UnitsAllocated = _unitsAllocated;
        SellAt = _sellAt;
        StopLoss = _stopLoss;
        WhenSold = _whenSold;
        SubOrderSequence = _subOrderSequence;
        ExecutedPrice = 0;
    }
}

#endregion

[Robot(AccessRights = AccessRights.FullAccess, AddIndicators = true, TimeZone = TimeZones.EasternStandardTime)]
public class fbdv7 : Robot
{
    #region Variables, Data Types etc
    [Parameter(DefaultValue = "Starting cbot !")]
    public string Message { get; set; }

    // Tick variables
    double _currentTickPrice;
    DateTime _currentServerTime;
    double _previousTickPrice;
    int _lastMinute = -1;
    double _minuteLow = double.MaxValue;
    int _currentMinute;
    bool saveTickData = false;
    int _hourStart = 9;
    int _minStart = 30;
    int _secStart = 0;
    int _hourEnd = 16;
    int _minEnd = 30;
    int _secEnd = 0;

    // Print control variables
    private bool _hasPrintedEOD = false;

    // Variables that control Low Prices including time constraints on hunting for them
    private Dictionary<long, LowPrice> _dicLowPrices;
    bool _restrictLowPriceHuntingPeriod;
    int _expiryMinutes;
    int _expirationStrategy;

    // Variables that control Sig Lows
    double _sigLowConfirmationMarker;
    int _significantLowExpirationStrategy; // 1 = EOD, !1 = add time to time found.
    int _significantLowExpirationMinute;
    int _significantLowHourEnd = 16;
    int _significantLowMinEnd = 30;
    int _significantLowSecEnd = 0;
    double _ride = 0;
    //private Dictionary<int, SignificantLow> _dicSignificantLows;

    // Variables that control Buying
    double _unitsBought;
    //private Dictionary<int, BuyOrder> _dicBuyOrders;
    #endregion

    #region On Start
    protected override void OnStart()
    {
        // To learn more about cTrader Algo visit our Help Center:
        // https://help.ctrader.com/ctrader-algo/

        // Initialize variables for Low Price
        _dicLowPrices = new Dictionary<long, LowPrice>();
        _restrictLowPriceHuntingPeriod = true;
        _expiryMinutes = 360;
        _expirationStrategy = 1;

        // Initialize Significant Low variables
        _sigLowConfirmationMarker = 15;
        _significantLowExpirationMinute = 240;
        _significantLowExpirationStrategy = 1;
        //_dicSignificantLows = new Dictionary<int, SignificantLow>(); // Initialize here

        // Initialize Buy variables
        _unitsBought = 100;
        //_dicBuyOrders = new Dictionary<int, BuyOrder>(); // Initialize here

    }
    #endregion

    #region On Tick
    protected override void OnTick()
    {

        _currentServerTime = Server.Time;
        _currentTickPrice = Math.Round(Symbol.Bid, Symbol.Digits);
        _currentMinute = _currentServerTime.Minute;

        // Any tick that is not within a time range will not be entertained for finding new Low Prices, new significant Lows
        // EXCEPTION: If there are selling opportunities, we will sell.
        bool isInsideTradingHours = isWithinTimeRange(_hourStart, _minStart, _secStart, _hourEnd, _minEnd, _secEnd);

        #region Printing Output
        // The time is outside of our processing limits
        if (isInsideTradingHours == false)
        {
            TimeSpan now = Server.Time.TimeOfDay;
            TimeSpan endTime = new TimeSpan(_hourEnd, _minEnd, _secEnd);

            // If AFTER the end time and we havent printed the report ....
            if (now > endTime && !_hasPrintedEOD)
            {
                PrintDictionaryContents();
                _hasPrintedEOD = true;
            }
            return;
        }
        #endregion

        #region Inside Trading Hours
        if (isInsideTradingHours)
        {

            _hasPrintedEOD = false;
            // *** check for Selling 
            checkSellingOpportunities(_currentTickPrice, _currentServerTime);

            #region Aggregate Lowest Price per minute
            if (_currentMinute != _lastMinute)
            {

                if (_lastMinute != -1 && _minuteLow != double.MaxValue)
                {
                    manageLowPrices(_minuteLow, _currentServerTime.AddMinutes(-1));
                }

                _lastMinute = _currentMinute;
                _minuteLow = _currentTickPrice;
            }

            else
            {

                if (_currentTickPrice < _minuteLow)
                {
                    _minuteLow = _currentTickPrice;
                }
            }
            #endregion

            #region check for Sig Lows
            manageSignificantLows(_currentTickPrice, _currentServerTime);
            #endregion

        }
        #endregion
    }
    #endregion

    #region Check Selling opportunities
    protected void checkSellingOpportunities(double _currentTickPrice, DateTime _currentServerTime)
    {
        try
        {
            foreach (var entry in _dicLowPrices)
            {
                LowPrice _lowPriceObject = entry.Value;

                // Only process if there is a BuyOrder and it is currently ACTIVE
                if (_lowPriceObject.AssociatedSignificantLow?.BuyOrder != null &&
                    _lowPriceObject.AssociatedSignificantLow.BuyOrder.State.Equals("ACTIVE"))
                {
                    BuyOrder _buyOrder = _lowPriceObject.AssociatedSignificantLow.BuyOrder;
                    int _activeSubOrder = _buyOrder.ActiveSubOrder;

                    // Ensure the active index exists in our dictionary
                    if (!_buyOrder.SubOrders.ContainsKey(_activeSubOrder)) continue;

                    SubOrder _currentSub = _buyOrder.SubOrders[_activeSubOrder];

                    // --- SCENARIO 1: STOP LOSS HIT (Liquidate EVERYTHING) ---
                    if (_currentTickPrice <= _currentSub.StopLoss)
                    {
                        // Loop through ALL sub-orders and calculate loss individually for pending ones
                        foreach (var subEntry in _buyOrder.SubOrders)
                        {
                            SubOrder pendingSub = subEntry.Value;

                            // Only apply the loss to units that haven't been sold yet
                            if (pendingSub.WhenSold == DateTime.MinValue)
                            {
                                // Using _pricePaid to calculate true financial loss per slice
                                pendingSub.Profit = pendingSub.UnitsAllocated * (_currentTickPrice - _buyOrder.PricePaid);
                                pendingSub.WhenSold = _currentServerTime;
                                pendingSub.ExecutedPrice = _currentTickPrice;
                            }
                        }

                        _buyOrder.UnitsInHand = 0;
                        _buyOrder.State = "EXPIRED";
                        _buyOrder.Log = $"Buy order #{_buyOrder.Id} liquidated. SL hit at {_currentTickPrice}.";

                        continue;
                    }

                    // --- SCENARIO 2: TAKE PROFIT HIT (Sell current slice) ---
                    if (_currentTickPrice >= _currentSub.SellAt)
                    {
                        // Calculate profit for just this slice using _pricePaid
                        _currentSub.Profit = _currentSub.UnitsAllocated * (_currentTickPrice - _buyOrder.PricePaid);
                        _currentSub.WhenSold = _currentServerTime;
                        _currentSub.ExecutedPrice = _currentTickPrice;

                        // Reduce the units remaining in hand
                        _buyOrder.UnitsInHand -= _currentSub.UnitsAllocated;

                        // Move to the next SubOrder or Close the order entirely
                        if (_buyOrder.SubOrders.ContainsKey(_activeSubOrder + 1))
                        {
                            _buyOrder.ActiveSubOrder++;
                        }
                        else
                        {
                            _buyOrder.State = "CLOSED";
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Print($"Error in checkSellingOpportunities: {ex.Message}");
        }
    }
    #endregion

    #region Check for Sig Lows
    protected void manageSignificantLows(double _currentTickPrice, DateTime _currentServerTime)
    {

        try
        {

            foreach (var _entry in _dicLowPrices)
            {

                LowPrice _lowPriceObject = _entry.Value;
                bool _hasConvertedToSigLow = _lowPriceObject.ConvertedToSigLow;
                string _currentState = _lowPriceObject.Status.ToUpper();

                if (_lowPriceObject.ConvertedToSigLow)
                {

                    SignificantLow _tempSigLowObject = _lowPriceObject.AssociatedSignificantLow;
                    string _tempSigLowObjectState = _tempSigLowObject.State.ToUpper();

                    // 1. Check if the Sig Low has reached its expiry and if it is active, then set it to EXPIRED
                    bool isExpired = hasExpired(_currentServerTime, _tempSigLowObject.ExpirationTime);

                    if (isExpired && _tempSigLowObject.State.Equals("ACTIVE"))
                    {

                        _tempSigLowObject.State = "EXPIRED";
                        _tempSigLowObject.Log = $"Sig Low # {_tempSigLowObject.Id} expired at {_currentServerTime}";
                        continue;

                    }

                    // 2. Check for flush - Rule: If the ticker is less than the Floor price AND sig low is active, set state = expired, log the flush tick and time it happened
                    if (_tempSigLowObjectState.Equals("ACTIVE") && isFlush(_currentTickPrice, _lowPriceObject.AssociatedSignificantLow.Floor))
                    {

                        _lowPriceObject.AssociatedSignificantLow.State = "EXPIRED";
                        _lowPriceObject.AssociatedSignificantLow.Log = $"Sig Low # {_tempSigLowObject.Id} flushed at {_currentServerTime} when ticker dropped to {_currentTickPrice}";
                        continue;
                    }

                    // 3. Has it reached a new max breach
                    if (_tempSigLowObjectState.Equals("ACTIVE") && _currentTickPrice <= _tempSigLowObject.Ceiling && _currentTickPrice >= _tempSigLowObject.Floor)
                    {

                        _tempSigLowObject.HasBreachedDown = true;
                        _tempSigLowObject.DownBreachTickerPrice = _currentTickPrice;
                        _tempSigLowObject.DownBreachTime = _currentServerTime;

                        if (_tempSigLowObject.MaxBreach > _currentTickPrice)
                        {
                            _tempSigLowObject.MaxBreach = _currentTickPrice;

                            _tempSigLowObject.Log = Environment.NewLine + $"Sig Low # {_tempSigLowObject.Id} breached downwards at {_currentServerTime} when ticker was {_currentTickPrice}";
                        }

                        

                        continue;
                    }

                    // 4. Upwards breach resulting in BUY
                    if (_tempSigLowObjectState.Equals("ACTIVE") && _tempSigLowObject.HasBreachedDown && _currentTickPrice >= _tempSigLowObject.BuyAt)
                    {

                        _tempSigLowObject.State = "CONVERTED";
                        _tempSigLowObject.HasBreachedUp = true;
                        _tempSigLowObject.UpBreachTickerPrice = _currentTickPrice;
                        _tempSigLowObject.UpBreachTime = _currentServerTime;
                        _tempSigLowObject.Log += Environment.NewLine + $"Sig Low # {_tempSigLowObject.Id} converted to a BUY at {_currentServerTime} when ticker was {_currentTickPrice}";

                        // Buy Order
                        BuyOrder _newBuyOrder = createNewBuyOrder(_tempSigLowObject, _currentServerTime, _currentTickPrice);

                        if (_newBuyOrder is null) throw new Exception($"_newBuyOrder is null.");

                        _tempSigLowObject.BuyOrder = _newBuyOrder;
                        _tempSigLowObject.State = "CONVERTED";

                        continue;
                    }
                }

                else
                {

                    double _ride = _currentTickPrice - _lowPriceObject.Price;

                    if (_ride >= _sigLowConfirmationMarker)
                    {

                        SignificantLow _newSignificantLowObject = CreateNewSignificantLow(_lowPriceObject, _currentTickPrice, _currentServerTime, _significantLowHourEnd, _significantLowMinEnd, _significantLowSecEnd);

                        if (_newSignificantLowObject is null) throw new Exception();

                        // Update original low price object flags
                        _lowPriceObject.AssociatedSignificantLow = _newSignificantLowObject;
                        _lowPriceObject.ConvertedToSigLow = true;
                        _lowPriceObject.Status = "CONVERTED";

                        continue;
                    }
                }

            }
        }
        catch (Exception ex)
        {
            Print($"Exception in manageSignificantLows at {_currentServerTime} | Ticker Price {_currentTickPrice} | Reason: {ex.Message}");
            return;
        }
    }
    #endregion

    #region Create New Buy BuyOrder
    protected BuyOrder createNewBuyOrder(SignificantLow _sigLowObject, DateTime _currentServerTime, double _pricePaid)
    {

        try
        {
            double _gap = _pricePaid - _sigLowObject.MaxBreach;
            BuyOrder _newBuyOrder = new BuyOrder(this, _sigLowObject, _currentServerTime, _pricePaid, _gap, _unitsBought);
            return _newBuyOrder;
        }

        catch (Exception ex)
        {

            Print($"Exception in createNewBuyOrder at {_currentServerTime} | Ticker Price {_currentTickPrice} | Reason: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Has Sig Low Flushed
    protected bool isFlush(double _currentTickPrice, double _sigLowFlushPrice)
    {

        try
        {

            if (_currentTickPrice < _sigLowFlushPrice)
            {
                return true;
            }

            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {

            Print($"Exception in isFlush at {_currentServerTime} | Ticker Price {_currentTickPrice} | Reason: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region Create New Significant Low
    protected SignificantLow CreateNewSignificantLow(LowPrice _lowPriceObject, double _currentTickPrice, DateTime _currentServerTime, int _significantLowHourEnd, int _significantLowMinEnd, int _significantLowSecEnd)
    {

        try
        {
            SignificantLow _newSignificantLow = new SignificantLow(_lowPriceObject.Id, _lowPriceObject.Price, _currentServerTime, _currentTickPrice, _significantLowExpirationStrategy, _significantLowExpirationMinute,
                _significantLowHourEnd, _significantLowMinEnd, _significantLowSecEnd);
            return _newSignificantLow;
        }
        catch (Exception ex)
        {
            Print($"Exception in CreateNewSignificantLow at {_currentServerTime} | Ticker Price {_currentTickPrice} | Reason: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Manage Low Prices
    protected void manageLowPrices(double _currentTickPrice, DateTime _currentServerTime)
    {

        try
        {

            // Check for all lowPriceObjects that have state = active, and have hit their expiry time.
            // For all such objects, set the state = expired
            foreach (var _lowPriceObject in _dicLowPrices)
            {

                string _status = _lowPriceObject.Value.Status.ToUpper();
                DateTime _expiryTime = _lowPriceObject.Value.ExpiryTime;
                bool isExpired;

                if (_status.Equals("ACTIVE"))
                {
                    isExpired = hasExpired(_currentServerTime, _expiryTime);
                    if (isExpired)
                    {
                        _lowPriceObject.Value.Status = "EXPIRED";
                        _lowPriceObject.Value.Log = _lowPriceObject.Value.Log + $"\t\t\t\tExpired at {_currentServerTime}";
                    }
                }
                continue;
            }

            // Baseline setting of low price array
            if (_dicLowPrices.Count == 0)
            {

                LowPrice _newLowPriceObject = createNewLowPriceObject(_currentTickPrice, _currentServerTime);

                if (_newLowPriceObject is null) throw new Exception("_newLowPriceObject is null");

                addLowPriceToList(_newLowPriceObject);

                // For logging purpose
                string _formattedCurrentTickPrice = _currentTickPrice.ToString("F" + Symbol.Digits);
                _previousTickPrice = _currentTickPrice;
                return;
            }

            // If current price > prev price, we continue since there is no dip and therefore no new Low Price
            if (_currentTickPrice >= _previousTickPrice)
            {
                _previousTickPrice = _currentTickPrice;
                return;
            }

            // If current ticker is less than previous ticker, we have a dip and this needs to be noted as a Low Price Object
            if (_currentTickPrice < _previousTickPrice)
            {
                LowPrice _newLowPriceObject = createNewLowPriceObject(_currentTickPrice, _currentServerTime);

                if (_newLowPriceObject is null) throw new Exception("_newLowPriceObject is null");

                addLowPriceToList(_newLowPriceObject);
                // For logging
                string _formattedCurrentTickPrice = _currentTickPrice.ToString("F" + Symbol.Digits);
                string _formattedPreviousTickPrice = _previousTickPrice.ToString("F" + Symbol.Digits);
                _previousTickPrice = _currentTickPrice;
                return;
            }
        }
        catch (Exception ex)
        {

            Print($"Exception in manageLowPrices at {_currentServerTime} | Ticker Price {_currentTickPrice} | Reason: {ex.Message}");
            return;
        }
    }
    #endregion

    #region check an objects ExpirationStrategy
    protected bool hasExpired(DateTime _currentServerTime, DateTime _expiryTime)
    {
        return _currentServerTime > _expiryTime;
    }
    #endregion

    #region Create New LowPriceObject
    protected LowPrice createNewLowPriceObject(double _currentTickPrice, DateTime _currentServerTime)
    {

        try
        {
            long _lowPriceId = generateId(_currentServerTime);
            LowPrice _newLowPriceObject = new LowPrice(_currentTickPrice, _currentServerTime, _expirationStrategy, _expiryMinutes, _lowPriceId, Symbol.Digits, _hourEnd, _minEnd, _secEnd);
            return _newLowPriceObject;
        }
        catch (Exception ex)
        {
            Print($"Exception in createNewLowPriceObject at {_currentServerTime} | Ticker Price {_currentTickPrice} | Reason: {ex.Message}");
            return null;
        }
    }
    #endregion

    #region Generate Ids for Low Prices
    protected long generateId(DateTime _currentServerTime)
    {

        string output = _currentServerTime.ToString("yyyyMMdd-HHmmss");
        output = output.Replace("-", "");

        return long.Parse(output);
    }
    #endregion

    #region Add LowPriceObject to list
    protected void addLowPriceToList(LowPrice _newLowPriceObject)
    {
        _dicLowPrices.Add(_newLowPriceObject.Id, _newLowPriceObject);
    }
    #endregion

    #region Time Range Check
    private bool isWithinTimeRange(int _hourStart, int _minStart, int _secStart, int _hourEnd, int _minEnd, int _secEnd)
    {
        TimeSpan now = Server.Time.TimeOfDay;
        TimeSpan start = new TimeSpan(_hourStart, _minStart, _secStart);
        TimeSpan end = new TimeSpan(_hourEnd, _minEnd, _secEnd);
        return now >= start && now <= end;
    }
    #endregion

    #region Print Dictionary
    private void PrintDictionaryContents()
    {
        // 1. Define the directory path and create the dynamic file name
        string folderPath = @"C:\Users\usman\OneDrive\Desktop\amfaus";
        string fileName = $"fbdv5_Report_{_currentServerTime.ToString("yyyyMMdd-HHmmss")}.txt";
        string filePath = Path.Combine(folderPath, fileName);

        // 2. Ensure the directory exists
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 3. Separate the LowPrices into two lists for sorting
        List<LowPrice> withBuyOrders = new List<LowPrice>();
        List<LowPrice> withoutBuyOrders = new List<LowPrice>();

        foreach (var entry in _dicLowPrices.Values)
        {
            if (entry.AssociatedSignificantLow != null && entry.AssociatedSignificantLow.BuyOrder != null)
            {
                withBuyOrders.Add(entry);
            }
            else
            {
                withoutBuyOrders.Add(entry);
            }
        }

        // 4. Create an array of groups to easily print headers and loop through both lists
        var outputGroups = new[]
        {
        new { Title = "=== SECTION 1: EXECUTED TRADES (WITH BUY ORDERS) ===", Items = withBuyOrders },
        new { Title = "=== SECTION 2: TRACKED LEVELS (NO BUY ORDERS) ===", Items = withoutBuyOrders }
    };

        try
        {
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                string header1 = "--------------------------------------------------------------------------------";
                string header2 = $"--- END OF SESSION REPORT: {Server.Time} ---";

                Print(header1); writer.WriteLine(header1);
                Print(header2); writer.WriteLine(header2);
                Print(header1); writer.WriteLine(header1);

                // Loop through our two groups
                foreach (var group in outputGroups)
                {
                    // Print the Section Header
                    Print(""); writer.WriteLine("");
                    Print(group.Title); writer.WriteLine(group.Title);
                    Print(header1); writer.WriteLine(header1);

                    // Loop through the LowPrices inside this specific group
                    foreach (LowPrice lp in group.Items)
                    {
                        // --- 1. LOW PRICE LEVEL ---
                        string lpLine = $"LOW PRICE #{lp.Id} | Price: {lp.Price} | Found: {lp.TimeFound} | Status: {lp.Status}";
                        Print(lpLine); writer.WriteLine(lpLine);

                        if (!string.IsNullOrEmpty(lp.Log))
                        {
                            Print($"\tLog: {lp.Log}"); writer.WriteLine($"\tLog: {lp.Log}");
                        }

                        // --- 2. SIGNIFICANT LOW LEVEL ---
                        SignificantLow sig = lp.AssociatedSignificantLow;
                        if (sig != null)
                        {
                            string sigLine = $"\t[SIG LOW] ID: {sig.Id} | Value: {sig.Price} | Marker: {sig.MarkerPrice} | Confirmed: {sig.TimeFound} | Ceiling: {sig.Ceiling} | Floor : {sig.Floor} | BuyAt: {sig.BuyAt} | FlushAt: {sig.Floor} | Max Breach :{sig.MaxBreach}";
                            //sigLine = sigLine + $"\t\t" + sig.Log;
                            Print(sigLine); writer.WriteLine(sigLine);

                            // --- 3. BUY ORDER LEVEL ---
                            if (sig.BuyOrder != null)
                            {
                                BuyOrder buy = sig.BuyOrder;
                                string buyLine = $"\t\t[BUY ORDER] ID: {buy.Id} | Entry: {buy.PricePaid} | Total Units: {buy.UnitsBought} | Gap: {buy.Gap}";
                                Print(buyLine); writer.WriteLine(buyLine);

                                // --- 4. SUB ORDERS (DICTIONARY) ---
                                if (buy.SubOrders != null)
                                {
                                    foreach (var subEntry in buy.SubOrders)
                                    {
                                        int seq = subEntry.Key;
                                        SubOrder sub = subEntry.Value;

                                        // Format time and price if executed, otherwise display N/A
                                        bool isExecuted = sub.WhenSold != DateTime.MinValue;
                                        string subStatus = isExecuted ? "EXECUTED" : "PENDING/CANCELLED";
                                        string timeStr = isExecuted ? sub.WhenSold.ToString("HH:mm:ss") : "N/A";
                                        string priceStr = isExecuted ? sub.ExecutedPrice.ToString() : "N/A";

                                        double roundedProfit = Math.Round(sub.Profit, 2);

                                        string subLine = $"\t\t\t|_ Sub {seq} | Units: {sub.UnitsAllocated} | TP: {sub.SellAt} | SL: {sub.StopLoss} | Profit: {roundedProfit} ({subStatus} @ {priceStr} on {timeStr})";
                                        Print(subLine); writer.WriteLine(subLine);
                                    }
                                }

                                // --- 5. TOTAL PROFIT SUMMARY ---
                                string summaryLine = $"\t\t\t>>> TOTAL REALIZED PROFIT FOR ORDER #{buy.Id}: {buy.TotalProfit} <<<";
                                string divider = "\t\t\t" + new string('-', summaryLine.Length - 3);

                                Print(divider); writer.WriteLine(divider);
                                Print(summaryLine); writer.WriteLine(summaryLine);
                                Print(divider); writer.WriteLine(divider);
                            }
                        }
                        Print(""); writer.WriteLine(""); // Spacing between objects
                    }
                }

                Print(header1); writer.WriteLine(header1);
                Print($"Report successfully saved to: {filePath}");
            }
        }
        catch (Exception ex)
        {
            Print($"Error writing report to file: {ex.Message}");
        }
    }
    #endregion

    #region OnStop
    protected override void OnStop()
    {
        // Handle cBot stop here
    }
    #endregion
}
