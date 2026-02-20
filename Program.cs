using System;
using System.IO;
using System.Globalization;
using cAlgo.Robots; // Your bot's namespace

class Program
{
    static void Main(string[] args)
    {
        // 1. SETUP THE BOT
        Console.WriteLine("Initializing fbdv4 for CSV Replay...");
        fbdv5 bot = new fbdv5();

        // Initialize the Mock API objects
        // Note: The Mock Robot constructor already created Symbol and Server objects

        // Call OnStart()
        bot.ExecuteStart();
        Console.WriteLine("Bot Started.");

        // 2. READ THE CSV
        // Update this path to where you saved your downloaded CSV
        string csvPath = @"C:\Users\usman\OneDrive\Desktop\amfaus\TickData_Sample.csv";

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"ERROR: CSV file not found at {csvPath}");
            return;
        }

        string[] lines = File.ReadAllLines(csvPath);

        // 3. LOOP THROUGH TICKS
        // Skip header row (i=1)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            string[] parts = line.Split(',');

            // --- PARSING LOGIC ---
            // Based on your format: "2025-01-27 1:18:02 PM, 6069.8"
            // OR "2025-01-26 19:00:00.926,6069.8" (depending on which version you saved)

            string timeString = parts[0];
            string priceString = parts[1]; // Assuming Price/Bid is column 2

            DateTime tickTime;
            double tickPrice;

            // Try parsing generic format first, then specific
            if (!DateTime.TryParse(timeString, out tickTime))
            {
                // Fallback for custom formats if needed
                DateTime.TryParseExact(timeString, "yyyy-MM-dd h:mm:ss tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out tickTime);
            }

            double.TryParse(priceString, out tickPrice);

            // --- FEED THE BOT ---
            // Update the Mock Server Time and Symbol Price
            bot.Server.Time = tickTime;
            bot.Symbol.Bid = tickPrice;

            // Trigger the OnTick event
            bot.ExecuteTick();

            // Optional: Print progress every 100 ticks
            if (i % 100 == 0) Console.Write(".");
        }

        bot.ExecuteStop();
        Console.WriteLine("\nBacktest Complete.");
        Console.ReadLine();
    }
}
