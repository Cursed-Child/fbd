using System;
using System.Collections.Generic;

// We fake the namespace so your robot code doesn't need to change!
namespace cAlgo.API
{
    // 1. Mock Attributes
    public class RobotAttribute : Attribute
    {
        public AccessRights AccessRights { get; set; }
        public bool AddIndicators { get; set; }
        public TimeZones TimeZone { get; set; }
    }

    public class ParameterAttribute : Attribute
    {
        public object DefaultValue { get; set; }
    }

    public enum AccessRights { FullAccess, None }
    public enum TimeZones { EasternStandardTime, UTC }
    public enum RoundingMode { Down, Up, ToNearest }

    // 2. Mock Robot Base Class
    public class Robot
    {
        public Symbol Symbol { get; set; }
        public Server Server { get; set; }

        public Robot()
        {
            Symbol = new Symbol();
            Server = new Server();
        }

        protected virtual void OnStart() { }
        protected virtual void OnTick() { }
        protected virtual void OnStop() { }
        protected virtual void OnBar() { }

        // Public methods to trigger events from our Console App
        public void ExecuteStart() => OnStart();
        public void ExecuteTick() => OnTick();
        public void ExecuteStop() => OnStop();

        public void Print(string message, params object[] args)
        {
            Console.WriteLine($"[BOT LOG]: {string.Format(message, args)}");
        }
    }

    // 3. Mock Server & Symbol
    public class Server
    {
        public DateTime Time { get; set; }
    }

    public class Symbol
    {
        public double Bid { get; set; }
        public double Ask { get; set; }
        public int Digits { get; set; } = 5; // Default to 5 digits
        public string Name { get; set; } = "TEST-SYMBOL";

        public double NormalizeVolumeInUnits(double volume, RoundingMode mode)
        {
            // Simple mock: just round down to nearest 1000 or 0.01
            return Math.Floor(volume);
        }
    }

    // 4. Mock Collections & Indicators (if needed)
    namespace Collections { }
    namespace Indicators { }
    namespace Internals { }
}
