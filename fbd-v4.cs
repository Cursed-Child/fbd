using System;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using System.Collections.Generic;
using System.IO;

namespace cAlgo.Robots;

    public class SignificantLow{

        public int Id { get; set;}
        public double Price { get; set;}
        public DateTime TimeFound { get; set;}
        public bool IsActive { get; set; }
        public double MarkerPrice { get; set; }
        public string Log { get; set; }
        public double Ceiling { get; set; }
        public double Floor { get; set; }
        public double MaxBreach {get; set;}
        public bool HasBreachedDown {get; set;}
        public bool HasBreachedUp {get; set;}
        public double BuyAt { get; set;}
        public BuyOrder BuyOrder {get; set;}

        public SignificantLow(int _id, double _price, DateTime _timeFound, double _markerPrice){
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

            // Internal variable for logging
            double _ride = _markerPrice - _price;
            _ride = Math.Round(_ride,4);

            Log = $"Significant Low Id: {_id} |Price: {_price} |Confirmed: {_timeFound} |Marker: {_markerPrice} |Ride: {_ride} |Flush Price: {Floor}";
        }
    }

    #region Buy and Tranch
    public class BuyOrder {

        public int Id {get; set;}
        public double PricePaid {get; set;}
        public double UnitsBought {get; set;}
        public double UnitsInHand {get;set;}
        public double SellingPrice {get; set;}
        public double StopLoss {get; set;}
        public double OTO {get;set;}
        public string Log {get;set;}
        public DateTime TimeBought{get; set;}
        public int ActiveTranche { get; set;}
        public double TrancheRatio {get; set;}
        public double ProfitLoss {get; set;}
        public bool IsActive { get;set;}
        public double MaxBreach { get; set;}
        public List<Bucket> TrancheList {get; set;}

        public BuyOrder(int _id, double _pricePaid, double _unitsBought, double _stopLoss, double _oto, DateTime _timeBought, double _maxBreach, Symbol _symbol){

            Id = _id;
            PricePaid = _pricePaid;
            UnitsBought = _unitsBought;
            UnitsInHand = _unitsBought;
            SellingPrice = _oto;
            StopLoss = _stopLoss;
            OTO = _oto;
            TimeBought = _timeBought;
            ActiveTranche = 0; // To align with 0-index
            TrancheRatio = 0.30;
            IsActive = true;
            MaxBreach = _maxBreach;


            Log = $"Buy Order Id: {_id} |Time of Purchase {_timeBought} |Price Paid: {_pricePaid} |Units Bought: {_unitsBought} |Units Left: {_unitsBought} |OTO: {_oto} |Max Breach: {MaxBreach} |Sell at: {_oto} |Stop Loss: {_stopLoss}";

            // Added some logic for the new Tranch object and to create its array
            double [] _trancheRatio = {0.30,0.40,0.30};
            TrancheList = CreateTranchList(_trancheRatio, _unitsBought,_pricePaid,_oto,_stopLoss, _maxBreach, _symbol);
            
            // Added log string for new tranches array
            string _tmpTranchLogs = string.Empty + Environment.NewLine;

            for (int i=0; i < TrancheList.Count; i++){
                Bucket _tranchObj = TrancheList[i];
                _tmpTranchLogs = _tmpTranchLogs + $"{_tranchObj.Log}";
                _tmpTranchLogs = _tmpTranchLogs + Environment.NewLine;
            }

            Log = Log + _tmpTranchLogs;
        }

        protected List<Bucket> CreateTranchList(double[] _trancheRatio, double _unitsBought, double _pricePaid, double _oto, double _stopLoss, double _maxBreach, Symbol _symbol){

            List<Bucket> _returnList = new List<Bucket>();
            double _sellAt=0;
            double _tranchStopLoss=0;
            double _currentTranchRatio=0;

            for (int i=0; i<_trancheRatio.Length;i++){
                
                if (i==0){
                    _currentTranchRatio = _trancheRatio[i];
                    _sellAt = _oto;
                    _tranchStopLoss = _stopLoss;
                    Bucket _tranchObject = new Bucket(i+1,_currentTranchRatio,_unitsBought,_tranchStopLoss,_sellAt, _symbol);
                    _returnList.Add(_tranchObject);
                }

                if (i == 1){
                    _currentTranchRatio = _trancheRatio[i];
                    _sellAt = _sellAt + (_pricePaid - _maxBreach);
                    _tranchStopLoss = _pricePaid;
                    Bucket _tranchObject = new Bucket(i+1, _currentTranchRatio,_unitsBought,_tranchStopLoss, _sellAt,_symbol);
                    _returnList.Add(_tranchObject);
                }

                if (i == 2){

                    _currentTranchRatio = _trancheRatio[i];
                   _sellAt = _sellAt + 2*(_pricePaid - _maxBreach);
                   _tranchStopLoss = _oto;
                   Bucket _tranchObject = new Bucket(i+1, _currentTranchRatio,_unitsBought,_tranchStopLoss, _sellAt,_symbol);
                   _returnList.Add(_tranchObject);
                }
            }

            return _returnList;            
        }
    }

    public class Bucket {

        public int TrancheNumber { get; set;}
        public double TrancheVolumeRatio { get; set;}
        public double ActualUnits {get;set;}
        public double StopLoss { get; set;}
        public double SellAt { get; set;}
        public double Profit {get; set;}
        public DateTime WhenSold {get;set;}
        public string Log {get;set;}

        public Bucket(int _number, double _ratio, double _totalUnitsBought, double _stopLoss, double _sellAt, Symbol _symbol){

            TrancheNumber = _number;
            double _rawUnits = _ratio * _totalUnitsBought;
            ActualUnits = _symbol.NormalizeVolumeInUnits(_rawUnits, RoundingMode.Down);
            StopLoss = _stopLoss;
            SellAt = Math.Round(_sellAt,4);
            Log = $"        Tranch # {_number} | Will sell {ActualUnits} units @ {SellAt} | Stop Loss set at: {StopLoss}";
        }
    }
    #endregion

    #region Low Price 
    public class LowPrice
    {
        public double Price { get; set; }
        public DateTime TimeFound { get; set; }
        public bool ConvertedToSigLow { get; set; }
        public DateTime ExpiryTime { get; set; }
        public string Log { get; set; }
        public bool IsActive {get;set;}

        // Lows will have to expire eventually either because its EOD or they hit their ExpiryTime
        // EOD = 1, Decay (due to Expiry Time being reached) = 2
        public int ExpirationStrategy { get; set;}
        public int Sequence { get; set; }

        // Associated Significant Low
        public SignificantLow AssociatedSignificantLow { get; set;}

        // Added 'int expiryMinutes' to the constructor parameters
        public LowPrice(double _price, DateTime _timeFound, int _expirationStrategy, int _expiryMinutes, int _sequenceLowPrice, int _digits)
        {
            Price = _price;
            TimeFound = _timeFound;
            ConvertedToSigLow = false;            
            ExpiryTime = _timeFound.AddMinutes(_expiryMinutes); // This value will ONLY be used for zombie-ing a low price object if _expirationStrategy is NOT 1
            IsActive = true;
            Sequence = _sequenceLowPrice;

            // Added for logging 
            string _formattedPrice = _price.ToString("F" + _digits);

            if (_expirationStrategy == 1){
                Log = Environment.NewLine + $"Low Price # {_sequenceLowPrice} |Price: {_formattedPrice} |Found: {_timeFound} |Unless converts to Sig Low, expires at 430 pm";
            }
            else{
                Log = Environment.NewLine + $"Low Price # {_sequenceLowPrice} |Price: {_formattedPrice} |Found: {_timeFound} |Unless converts to Sig Low, expires on {ExpiryTime}";
            }
        }
    }
    #endregion

[Robot(AccessRights = AccessRights.FullAccess, AddIndicators = true, TimeZone = TimeZones.EasternStandardTime)]
public class fbdv4 : Robot
{
    [Parameter(DefaultValue = "Starting cBot")]
    public string Message { get; set; }

    // Tick variables
    double _currentTickPrice;
    DateTime _currentServerTime;
    double _previousTickPrice;
    int _lastMinute = -1;
    double _minuteLow = double.MaxValue;
    int _currentMinute;

    // Print control variables
    private bool _hasPrintedEOD = false;

    // Variables that control Low Prices including time constraints on hunting for them
    private Dictionary<int, LowPrice> _dicLowPrices;
    bool _restrictLowPriceHuntingPeriod;
    int _sequenceLowPrice;
    int _expiryMinutes;
    int _expirationStrategy;

    // Variables that control Sig Lows
    double _sigLowConfirmationMarker;
    private Dictionary<int, SignificantLow> _dicSignificantLows;

    // Variables that control Buying
    double _unitsBought;
    private Dictionary<int, BuyOrder> _dicBuyOrders;

    // Variables that control Selling
    #region On Start
    protected override void OnStart()
    {   

        // Initialize variables for Low Price
        _dicLowPrices = new Dictionary<int,LowPrice>();
        _restrictLowPriceHuntingPeriod = true;
        _sequenceLowPrice = 1;
        _expiryMinutes = 420;
        _expirationStrategy = 1;

        // Initialize Significant Low variables
        _sigLowConfirmationMarker = 15;
        _dicSignificantLows = new Dictionary<int, SignificantLow>(); // Initialize here

        // Initialize Buy variables
        _unitsBought = 100;
        _dicBuyOrders = new Dictionary<int, BuyOrder>(); // Initialize here
    }
    #endregion

    #region On Tick
    protected override void OnTick()
    {

        _currentServerTime = Server.Time;
        _currentTickPrice = Math.Round(Symbol.Bid,Symbol.Digits);
        _currentMinute = _currentServerTime.Minute;

        #region Printing Logic
        if (_currentServerTime.TimeOfDay >= new TimeSpan(16,30,0)){

            if (!_hasPrintedEOD){

                WriteResultsToFile();
                _hasPrintedEOD = true;
            }

            return;
        }

        if (_currentServerTime.TimeOfDay < new TimeSpan(16,30,0)){
            _hasPrintedEOD= false;
        }
        #endregion

        #region
        //checkSellingOpportunities(_currentTickPrice, _currentServerTime);
        #endregion

        #region Aggregate Lowest Price per minute
        if(_currentMinute != _lastMinute){

            if (_lastMinute != -1 && _minuteLow != double.MaxValue){
                manageLowPrices(_minuteLow,_currentServerTime.AddMinutes(-1));
            }

            _lastMinute = _currentMinute;
            _minuteLow = _currentTickPrice;
        }

        else{

            if (_currentTickPrice < _minuteLow){
                _minuteLow = _currentTickPrice;
            }
        }
        #endregion

        #region Manage Significant Lows
        manageSignificantLows(_currentTickPrice,_currentServerTime);
        #endregion

        return;
    }
    #endregion

    #region Check Selling Opps Using Tranch array
    protected void checkSellingOpportunities(double _currentTickPrice, DateTime _currentServerTime){

        foreach (var _entry in _dicBuyOrders){

            BuyOrder _buyOrderObject = _entry.Value;
            List<Bucket> _listTranch = _buyOrderObject.TrancheList;
            double _perUnitProfit = 0;
            double _totalProfit = 0;
            int _bucketNumber = 0;

            if (_buyOrderObject.IsActive){

                _bucketNumber = _buyOrderObject.ActiveTranche;

                if (_bucketNumber + 1 == _listTranch.Count) {
                    _buyOrderObject.IsActive = false;
                    continue;
                }

                double _unitsInHand = _buyOrderObject.UnitsInHand;
                double _pricePaid = _buyOrderObject.PricePaid;
                double _sellAt = _listTranch[_bucketNumber].SellAt;
                double _stopLoss = _listTranch[_bucketNumber].StopLoss;
                double _unitsToSell = _listTranch[_bucketNumber].ActualUnits;

                if (_currentTickPrice >= _sellAt){

                    _perUnitProfit = _currentTickPrice - _pricePaid;
                    _totalProfit = Math.Round(_unitsToSell * _perUnitProfit,4);
                    _listTranch[_bucketNumber].Profit = _totalProfit;
                    _listTranch[_bucketNumber].WhenSold = _currentServerTime;
                    _listTranch[_bucketNumber].Log += $"| At {_listTranch[_bucketNumber].WhenSold}, Tranche # {_listTranch[_bucketNumber].TrancheNumber} was sold at {_currentTickPrice} for a profit of {_totalProfit}" + Environment.NewLine;
                    
                    // increment bucket number
                    _buyOrderObject.ActiveTranche = _bucketNumber + 1;

                    // reduce number of units in hand .. this number is used for stop loss
                    _buyOrderObject.UnitsInHand = _buyOrderObject.UnitsBought - _unitsToSell;

                    // add this mini log to Buy Order Logs
                    _buyOrderObject.Log += _listTranch[_bucketNumber].Log;

                    continue;
                }
                
                if (_currentTickPrice <= _stopLoss){

                    if (_bucketNumber == 0) _unitsToSell = _unitsBought;  

                    _perUnitProfit = _currentTickPrice - _buyOrderObject.PricePaid;
                    _totalProfit = _buyOrderObject.UnitsInHand * _perUnitProfit;

                    // Update log for bucket
                    _listTranch[_bucketNumber].Profit = _totalProfit;
                    _listTranch[_bucketNumber].WhenSold = _currentServerTime;
                    _listTranch[_bucketNumber].Log += $"|  At {_listTranch[_bucketNumber].WhenSold} price dropped to {_currentTickPrice} and stop loss was triggered | Units Sold: {_buyOrderObject.UnitsInHand} | Profit: {_totalProfit}";

                    // Invalided the Buy Order
                    _buyOrderObject.IsActive = false;

                    _buyOrderObject.Log += _listTranch[_bucketNumber].Log;

                    continue;
                }
            }
        }
    }

    #endregion

    #region Manage Significant Low
    protected void manageSignificantLows(double _currentTickPrice, DateTime _currentServerTime){

        // Before we proceed to FIND more Sig Lows, we have to process and update the already existing ones.
        foreach (var _sigLowEntry in _dicSignificantLows){

            SignificantLow _sigLowObject = _sigLowEntry.Value;

            if(_sigLowObject.IsActive){

                // Critical Sig Low Checks here 
                // 1. Flush?
                double _sigLowFlushPrice = _sigLowObject.Floor;

                if (_sigLowFlushPrice > _currentTickPrice)
                {
                    _sigLowObject.IsActive = false;
                    _sigLowObject.Log = _sigLowObject.Log + Environment.NewLine + $" --- Flushed at: {_currentServerTime} |Flushed @ Current Price: {_currentTickPrice}";
                    continue;
                }

                // 2. Has it breached below its value?
                double _sigLowCeilingPrice = _sigLowObject.Ceiling;

                if(_currentTickPrice >= _sigLowFlushPrice && _currentTickPrice <= _sigLowCeilingPrice){

                    // this flag will keep getting set to true but that is ok. 
                    _sigLowObject.HasBreachedDown = true;

                    if(_sigLowObject.MaxBreach > _currentTickPrice)
                        _sigLowObject.MaxBreach = _currentTickPrice;
                    
                    //_sigLowObject.Log = _sigLowObject.Log + Environment.NewLine + $" --- Time Breached Down: {_currentServerTime} |Ticker Price: {_currentTickPrice} |Max Breach: {_sigLowObject.MaxBreach}";
                    continue;
                }

                // 3. Has it breached down, and now is breaking up again resulting in a buy ?
                if(_currentTickPrice >= _sigLowObject.BuyAt && _sigLowObject.HasBreachedDown && _sigLowObject.HasBreachedUp == false){

                    _sigLowObject.HasBreachedUp = true;
                    _sigLowObject.IsActive = false;
                    BuyOrder _newBuyOrder = CreateNewBuyOrder(_sigLowObject.Id,_currentTickPrice,_unitsBought,_sigLowObject.Price,_sigLowObject.MaxBreach,_currentServerTime);
                    _dicBuyOrders.Add(_newBuyOrder.Id,_newBuyOrder);
                    continue;
                    //_lowPriceObject.AssociatedSignificantLow.Log = _lowPriceObject.AssociatedSignificantLow.Log + Environment.NewLine + $"-- Buy Signal |Time Bought: {_currentServerTime} |Buy Price: {_currentTickPrice} | Units Bought: {_unitsBought}";
                }
            }
        }

        foreach (var _entry in _dicLowPrices){

            LowPrice _lowPriceObject = _entry.Value;

            // If a Low Price has already converted to sig low OR is inactive, ignore
            if(_lowPriceObject.ConvertedToSigLow || !_lowPriceObject.IsActive) continue;

            double _ride = _currentTickPrice - _lowPriceObject.Price;

            if (_ride >= _sigLowConfirmationMarker){
                SignificantLow _newSignificantLowObject  = CreateNewSignificantLow(_lowPriceObject,_currentTickPrice,_currentServerTime);

                // Add to dictionary
                _dicSignificantLows.Add(_newSignificantLowObject.Id,_newSignificantLowObject);

                // Update original low price object flags
                _lowPriceObject.ConvertedToSigLow = true;
                _lowPriceObject.IsActive = false;

                continue;
            }
        }
    }
    #endregion

    #region Create New Buy Order
    protected BuyOrder CreateNewBuyOrder(int _id, double _pricePaid, double _unitsBought, double _stopLoss, double _maxBreach, DateTime _timeBought){

        // Calculate OTO when Buy Order is first created
        double _oto = _pricePaid + (_pricePaid - _maxBreach);
        BuyOrder _newBuyOrder = new BuyOrder(_id,_pricePaid,_unitsBought,_stopLoss,_oto,_timeBought, _maxBreach, Symbol);
        return _newBuyOrder;
    }
    #endregion

    #region Create New Significant Low
    protected SignificantLow CreateNewSignificantLow(LowPrice _lowPriceObject, double _currentTickPrice, DateTime _currentServerTime){

        SignificantLow _newSignificantLow = new SignificantLow(_lowPriceObject.Sequence, _lowPriceObject.Price,_currentServerTime,_currentTickPrice);
        return _newSignificantLow;
    }
    #endregion

    #region Print Final
    private void PrintFinalReport()
    {
        Print("--------------------------------------------------------------------------------");
        Print("FBDV3 - Final Report for {0}", _currentServerTime.ToShortDateString());
        Print("--------------------------------------------------------------------------------");

        foreach (var entry in _dicLowPrices)
        {
            // 1. Get the LowPrice object
            LowPrice lp = entry.Value;
            
            // 2. Print the Low Price line (Cleaned)
            Print(lp.Log);

            // 3. Attempt to find the matching Significant Low in the other dictionary
            // We use lp.Sequence because that is the ID you used as the Key
            if (_dicSignificantLows.TryGetValue(lp.Sequence, out SignificantLow sigLow))
            {
                // 4. Print the Significant Low line if it exists
                Print(" --" + sigLow.Log);
            }
        }
        Print("--------------------------------------------------------------------------------");
    }
    #endregion

    #region Manage Low Prices
    protected void manageLowPrices(double _currentTickPrice, DateTime _currentServerTime){


        // If there are Low Prices that are Active = True (which also means they have not been turned to Sig Low) and the time is 430, 
        // they need to be made Active = False
        foreach (var _entry in _dicLowPrices){

            LowPrice _lowPriceObject = _entry.Value;
                 
            if (_currentServerTime.TimeOfDay >= new TimeSpan(16,30,0) && _lowPriceObject.ExpirationStrategy == 1){
                if (_lowPriceObject.IsActive)
                    {
                        _lowPriceObject.IsActive = false;
                        _lowPriceObject.Log = _lowPriceObject.Log + Environment.NewLine + $"             --- This Low Price will be deleted since its after 430 PM EST";
                    }
                 }
        }
        
        // We are only going to look for new low prices between 930 am - 430 pm EST  
        if (_restrictLowPriceHuntingPeriod){

            // If outside time range, continue
            if(isWithinTimeRange() == false){

                _sequenceLowPrice = 1;

                if (_dicLowPrices.Count>0)
                    _dicLowPrices.Clear();

                return;
            }

            //*** If it gets here, then we are INSIDE working hours

            // Baseline setting of low price array
            if (_dicLowPrices.Count == 0){
                LowPrice _newLowPriceObject = createNewLowPriceObject(_currentTickPrice, _currentServerTime);
                addLowPriceToList(_newLowPriceObject);
                _sequenceLowPrice = _sequenceLowPrice + 1;

                // For logging purpose
                string _formattedCurrentTickPrice = _currentTickPrice.ToString("F" + Symbol.Digits);
                //Print($"{_newLowPriceObject.Log} | Previous Ticker: N/A | Current Ticker: {_formattedCurrentTickPrice}");
                //Print($"{_newLowPriceObject.Log}");
                _previousTickPrice = _currentTickPrice;
                return;
            }

            // If current price > prev price, we continue since there is no dip and therefore no new Low Price
            if (_currentTickPrice >= _previousTickPrice){
                //Print($"Current ticker price of {_currentTickPrice} >= Previous ticker price of {_previousTickPrice}. No dip noted.");
                _previousTickPrice = _currentTickPrice;
                return;
            }

            // If current ticker is less than previous ticker, we have a dip and this needs to be noted as a Low Price Object
            if (_currentTickPrice < _previousTickPrice){
                LowPrice _newLowPriceObject = createNewLowPriceObject(_currentTickPrice,_currentServerTime);
                addLowPriceToList(_newLowPriceObject);
                _sequenceLowPrice = _sequenceLowPrice + 1;

                // For logging
                string _formattedCurrentTickPrice = _currentTickPrice.ToString("F" + Symbol.Digits);
                string _formattedPreviousTickPrice = _previousTickPrice.ToString("F" + Symbol.Digits);

                //Print($"{_newLowPriceObject.Log} | Previous Ticker: {_formattedPreviousTickPrice} > Current Ticker: {_formattedPreviousTickPrice}");
                //Print($"{_newLowPriceObject.Log} | Previous Ticker: {_previousTickPrice:F8} > Current Ticker: {_currentTickPrice:F8}");
                //Print($"{_newLowPriceObject.Log}");
                _previousTickPrice = _currentTickPrice;
                return;
            }
        }
    }
    #endregion

    #region Create New LowPriceObject
    protected LowPrice createNewLowPriceObject(double _currentTickPrice, DateTime _currentServerTime){
        
        LowPrice _newLowPriceObject = new LowPrice(_currentTickPrice,_currentServerTime,_expirationStrategy,_expiryMinutes, _sequenceLowPrice, Symbol.Digits);
        return _newLowPriceObject;
    }
    #endregion

    #region Add LowPriceObject to list
    protected void addLowPriceToList(LowPrice _newLowPriceObject){
        _dicLowPrices.Add(_newLowPriceObject.Sequence,_newLowPriceObject);
    }
    #endregion

    #region Time Range Check
    private bool isWithinTimeRange()
    {
        TimeSpan now = Server.Time.TimeOfDay;
        TimeSpan start = new TimeSpan(9, 30, 0);
        TimeSpan end = new TimeSpan(16, 30, 0);
        return now >= start && now <= end;
    }
    #endregion

    #region Write PrintFinalReport
    private void WriteResultsToFile()
    {
        // 1. Path Management
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string folderPath = Path.Combine(desktopPath, "amfaus");
        
        string logFilePath = Path.Combine(folderPath, "fbdv4_Final_Results.txt");
        string csvFilePath = Path.Combine(folderPath, "fbdv4_Full_Analysis.csv");

        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        try
        {
            // --- BLOCK A: TEXT REPORT ---
            using (StreamWriter writer = new StreamWriter(logFilePath, false))
            {
                writer.WriteLine("FBDV4 FULL EXECUTION REPORT");
                writer.WriteLine($"Execution: {DateTime.Now:yyyy-MM-dd HH:mm:ss} | Symbol: {SymbolName}");
                writer.WriteLine("--------------------------------------------------------------------------------");

                foreach (var entry in _dicLowPrices)
                {
                    int key = entry.Key;
                    writer.WriteLine(CleanSystemPrefix(entry.Value.Log));

                    if (_dicSignificantLows.TryGetValue(key, out SignificantLow sig))
                    {
                        writer.WriteLine(" --" + CleanSystemPrefix(sig.Log));

                        if (_dicBuyOrders.TryGetValue(key, out BuyOrder order))
                        {
                            writer.WriteLine("    [TRADE] " + CleanSystemPrefix(order.Log));
                        }
                    }
                    writer.WriteLine(""); 
                }
            }

            // --- BLOCK B: CSV EXCEL SHEET ---
            using (StreamWriter csv = new StreamWriter(csvFilePath, false))
            {
                // Header Row - Added Buy_Order_ID
                string header = "LP_ID,LP_Price,LP_TimeFound,Converted," +
                                "Sig_ID,Sig_Price,Confirmed_At,Marker_Price,Will_Flush_At,Ride," +
                                "Buy_Order_ID,Buy_Signal_Time,Buy_Signal_Price,Max_Breach";
                csv.WriteLine(header);

                foreach (var entry in _dicLowPrices)
                {
                    int key = entry.Key;
                    LowPrice lp = entry.Value;

                    // 1. Low Price Columns
                    string row = string.Format("{0},{1},{2},{3}", 
                        lp.Sequence, 
                        lp.Price, 
                        lp.TimeFound.ToString("yyyy-MM-dd HH:mm:ss"), 
                        lp.ConvertedToSigLow);

                    // 2. Significant Low Columns (Lookup)
                    if (_dicSignificantLows.TryGetValue(key, out SignificantLow sig))
                    {
                        double ride = Math.Round(sig.MarkerPrice - sig.Price, Symbol.Digits);
                        row += string.Format(",{0},{1},{2},{3},{4},{5}", 
                            sig.Id, 
                            sig.Price, 
                            sig.TimeFound.ToString("yyyy-MM-dd HH:mm:ss"), 
                            sig.MarkerPrice, 
                            sig.Floor, 
                            ride);

                        // 3. Buy Order Columns (Lookup) - Added ID here
                        if (_dicBuyOrders.TryGetValue(key, out BuyOrder order))
                        {
                            row += string.Format(",{0},{1},{2},{3}", 
                                order.Id,
                                order.TimeBought.ToString("yyyy-MM-dd HH:mm:ss"), 
                                order.PricePaid, 
                                order.MaxBreach);
                        }
                        else
                        {
                            row += ",,,,"; // 4 commas for the 4 buy order columns
                        }
                    }
                    else
                    {
                        // No Sig Low means no Buy Order. Buffer 10 commas total to fill the row
                        row += ",,,,,,,,,,"; 
                    }

                    csv.WriteLine(row);
                }
            }

            Print("Success: Full TEXT report and Signal CSV saved to Desktop/amfaus/");
        }
        catch (Exception ex)
        {
            Print("FILE ERROR: Ensure files are not open in Excel. Details: {0}", ex.Message);
        }
    }
    #endregion

    #region Clean System Prefix
    private string CleanSystemPrefix(string log)
    {
        // If the log was built with Environment.NewLine or other prefixes, 
        // this ensures only the custom message remains.
        string searchPattern = "Info | ";
        int index = log.IndexOf(searchPattern);
        return index == -1 ? log.Trim() : log.Substring(index + searchPattern.Length).Trim();
    }
    #endregion

    #region OnStop
    protected override void OnStop()
    {

    }
    #endregion
}
