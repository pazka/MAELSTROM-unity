using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    /**
     * "dateday","screen_name","nb_tweets","avg_followers_count","nb_account_if_others"
     * "2023-02-07","##OTHERS##","11966","999","8696"
     * "2023-02-07","RER_A","130","257043","1"
     * "2023-02-07","RERB","115","151203","1"
     * "2023-02-07","BFMTV","54","4547979","1"
     * "2023-02-07","Ligne13_RATP","49","58633","1"
     * "2023-02-07","ClientsRATP","32","68539","1"
     * "2023-02-07","BFMParis","22","118137","1"
     * "2023-02-07","Ligne4_RATP","21","46979","1"
     * "2023-02-07","Ligne8_RATP","17","53259","1"
     */
    public struct GhostNetDataPoint
    {
        public DateTime date;
        public string screen_name;
        public int nb_tweets;
        public int followers_count;
        public int nb_accounts_others;
        public float normalizedNbTweets;
        public float normalizedFollowersCount;
        public float normalizedDate;
        public float daynormalizedNbTweets;
        public float daynormalizedFollowersCount;
        public bool isAggregated; // only for the account named "##OTHERS##"
    }

    public struct GhostNetDataBound
    {
        public GhostNetDataPoint Min;
        public GhostNetDataPoint Max;
    }

    public class GhostNetDataLoader : MonoBehaviour
    {
        [Header("Data Settings")] [SerializeField]
        private TextAsset csvFile;

        private GhostNetDataBound _dataBounds;

        public GhostNetDataPoint[] Data { get; private set; }

        public GhostNetDataBound DataBounds => _dataBounds;
        public bool IsDataLoaded { get; private set; }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "GhostNetsScene") return;

            LoadData();
        }

        /// <summary>
        ///     Load data from CSV file
        /// </summary>
        public void LoadData()
        {
            if (csvFile == null)
            {
                AppLogger.LogError("CSV file is not assigned!");
                return;
            }

            AppLogger.Log("Loading ghostNetData");

            var lines = csvFile.text.Split('\n');
            var dataList = new List<GhostNetDataPoint>();

            // Skip header line
            var firstDataPoint = true;

            for (var i = 1; i < lines.Length; i++) // Skip header
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = line.Split(',');
                if (fields.Length < 4) continue;

                // Parse fields: dateday, screen_name, nb_tweets, followers_count
                var dateString = fields[0].Trim('"');
                if (!DateTime.TryParse(dateString, out var date)) continue;

                var screenName = fields[1].Trim('"');

                if (!int.TryParse(fields[2].Trim('"'), out var nbTweets)) continue;
                if (!int.TryParse(fields[3].Trim('"'), out var followersCount)) continue;
                if (!int.TryParse(fields[4].Trim('"'), out var nbAccountsOthers)) continue;

                var dataPoint = new GhostNetDataPoint
                {
                    date = date,
                    screen_name = screenName,
                    nb_tweets = nbTweets,
                    followers_count = followersCount,
                    nb_accounts_others = nbAccountsOthers,
                    isAggregated = screenName == "##OTHERS##"
                };

                if (firstDataPoint)
                {
                    _dataBounds.Min = dataPoint;
                    _dataBounds.Max = dataPoint;
                    firstDataPoint = false;
                }
                else
                {
                    if (dataPoint.nb_tweets < _dataBounds.Min.nb_tweets)
                        _dataBounds.Min.nb_tweets = dataPoint.nb_tweets;
                    if (dataPoint.nb_tweets > _dataBounds.Max.nb_tweets)
                        _dataBounds.Max.nb_tweets = dataPoint.nb_tweets;

                    if (dataPoint.followers_count < _dataBounds.Min.followers_count)
                        _dataBounds.Min.followers_count = dataPoint.followers_count;
                    if (dataPoint.followers_count > _dataBounds.Max.followers_count)
                        _dataBounds.Max.followers_count = dataPoint.followers_count;

                    if (dataPoint.date < _dataBounds.Min.date) _dataBounds.Min.date = dataPoint.date;
                    if (dataPoint.date > _dataBounds.Max.date) _dataBounds.Max.date = dataPoint.date;
                }

                // Check if data is in chronological order
                if (dataList.Count > 0 && dataPoint.date < dataList[dataList.Count - 1].date)
                    throw new Exception("Data is not in chronological order");

                // For regular accounts, add as-is
                dataList.Add(dataPoint);
            }

            Data = dataList.ToArray();
            NormalizeData();
            if (Config.Get("dataDump", false))
                DumpNormalizedDataToCSV();
            IsDataLoaded = true;

            AppLogger.Log($"Data Loaded: {Data.Length} data points");
            AppLogger.Log($"Data bounds: {_dataBounds.Min.date:yyyy-MM-dd} to {_dataBounds.Max.date:yyyy-MM-dd}");
            AppLogger.Log($"Tweets bounds: {_dataBounds.Min.nb_tweets} to {_dataBounds.Max.nb_tweets}");
            AppLogger.Log($"Followers bounds: {_dataBounds.Min.followers_count} to {_dataBounds.Max.followers_count}");
        }

        private void NormalizeData()
        {
            // Detect and cap outliers in followers count to prevent normalization skewing
            float cappedMaxFollowers = _dataBounds.Max.followers_count;
            var outlierThreshold = _dataBounds.Max.followers_count * 0.1f; // 10% of max

            // Find the second highest followers count to use as cap if max is an outlier
            var secondHighestFollowers = 0f;
            foreach (var dataPoint in Data)
                if (dataPoint.followers_count > secondHighestFollowers &&
                    dataPoint.followers_count < _dataBounds.Max.followers_count)
                    secondHighestFollowers = dataPoint.followers_count;

            // If the max is significantly larger than the second highest, cap it
            if (secondHighestFollowers > 0 && _dataBounds.Max.followers_count > secondHighestFollowers * 10f)
            {
                cappedMaxFollowers = secondHighestFollowers;
                AppLogger.Log(
                    $"Capped followers normalization: Original max {_dataBounds.Max.followers_count:N0} -> Capped max {cappedMaxFollowers:N0} (outlier detected)");
            }

            // Pre-calculate logarithmic ranges for efficiency
            var logMinTweets = (float)Math.Log(_dataBounds.Min.nb_tweets + 1);
            var logMaxTweets = (float)Math.Log(_dataBounds.Max.nb_tweets + 1);
            var logMinFollowers = (float)Math.Log(_dataBounds.Min.followers_count + 1);
            var logMaxFollowers = (float)Math.Log(cappedMaxFollowers + 1);
            float dateRange = _dataBounds.Max.date.Ticks - _dataBounds.Min.date.Ticks;

            // Process data in chronological order, grouping by date
            var i = 0;
            while (i < Data.Length)
            {
                var currentDate = Data[i].date.Date;
                var dayStartIndex = i;

                // Find all data points for the same day
                while (i < Data.Length && Data[i].date.Date == currentDate) i++;
                var dayEndIndex = i;

                // Calculate min/max tweets count and follower count for this day only once
                float maxDayTweets = 0;
                var minDayTweets = float.MaxValue;
                float maxDayFollowers = 0;
                var minDayFollowers = float.MaxValue;
                for (var j = dayStartIndex; j < dayEndIndex; j++)
                {
                    if (Data[j].nb_tweets > maxDayTweets) maxDayTweets = Data[j].nb_tweets;
                    if (Data[j].nb_tweets < minDayTweets) minDayTweets = Data[j].nb_tweets;
                    if (Data[j].followers_count > maxDayFollowers) maxDayFollowers = Data[j].followers_count;
                    if (Data[j].followers_count < minDayFollowers) minDayFollowers = Data[j].followers_count;
                }

                // Calculate logarithmic ranges for this day
                var logMinDayTweets = (float)Math.Log(minDayTweets + 1);
                var logMaxDayTweets = (float)Math.Log(maxDayTweets + 1);
                var logMinDayFollowers = (float)Math.Log(minDayFollowers + 1);
                var logMaxDayFollowers = (float)Math.Log(maxDayFollowers + 1);

                // Normalize all data points for this day
                for (var j = dayStartIndex; j < dayEndIndex; j++)
                {
                    if (Data[j].screen_name == "##OTHERS##") continue;

                    // Logarithmic normalization for the day
                    var logCurrentDayTweets = (float)Math.Log(Data[j].nb_tweets + 1);
                    var logCurrentDayFollowers = (float)Math.Log(Data[j].followers_count + 1);

                    Data[j].daynormalizedNbTweets = logMaxDayTweets > logMinDayTweets
                        ? (logCurrentDayTweets - logMinDayTweets) / (logMaxDayTweets - logMinDayTweets)
                        : 0;
                    Data[j].daynormalizedFollowersCount = logMaxDayFollowers > logMinDayFollowers
                        ? (logCurrentDayFollowers - logMinDayFollowers) / (logMaxDayFollowers - logMinDayFollowers)
                        : 0;

                    // Logarithmic normalization for the entire data set
                    var logCurrentTweets = (float)Math.Log(Data[j].nb_tweets + 1);
                    var logCurrentFollowers = (float)Math.Log(Data[j].followers_count + 1);

                    Data[j].normalizedNbTweets = logMaxTweets > logMinTweets
                        ? (logCurrentTweets - logMinTweets) / (logMaxTweets - logMinTweets)
                        : 0;
                    Data[j].normalizedFollowersCount = logMaxFollowers > logMinFollowers
                        ? (logCurrentFollowers - logMinFollowers) / (logMaxFollowers - logMinFollowers)
                        : 0;

                    // Clamp normalized values to prevent values > 1.0
                    if (Data[j].normalizedNbTweets > 1.0f)
                        Data[j].normalizedNbTweets = 1.0f;
                    if (Data[j].normalizedFollowersCount > 1.0f)
                        Data[j].normalizedFollowersCount = 1.0f;

                    // Linear normalization for time (as requested)
                    Data[j].normalizedDate = (Data[j].date.Ticks - _dataBounds.Min.date.Ticks) / dateRange;
                }
            }

            AppLogger.Log(
                $"Data normalized with logarithmic scaling (optimized) - Followers range: {_dataBounds.Min.followers_count:N0} to {cappedMaxFollowers:N0}");
        }

        /// <summary>
        ///     Dump normalized data to CSV file for analysis
        /// </summary>
        private void DumpNormalizedDataToCSV()
        {
            try
            {
                var fileName = $"ghostNet_normalized_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (var writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine(
                        "date\treal_date\tscreen_name\tnb_tweets\tfollowers_count\tnb_accounts_others\tisAggregated\t" +
                        "normalizedNbTweets\tnormalizedFollowersCount\tnormalizedDate\tdaynormalizedNbTweets\tdaynormalizedFollowersCount");

                    // Write data
                    foreach (var dataPoint in Data)
                        writer.WriteLine($"{dataPoint.date:yyyy-MM-dd HH:mm:ss}\t" +
                                         $"{dataPoint.date:yyyy-MM-dd HH:mm:ss}\t" +
                                         $"\"{dataPoint.screen_name}\"\t" +
                                         $"{dataPoint.nb_tweets}\t" +
                                         $"{dataPoint.followers_count}\t" +
                                         $"{dataPoint.nb_accounts_others}\t" +
                                         $"{dataPoint.isAggregated.ToString().ToLower()}\t" +
                                         $"{dataPoint.normalizedNbTweets:F6}\t" +
                                         $"{dataPoint.normalizedFollowersCount:F6}\t" +
                                         $"{dataPoint.normalizedDate:F6}\t" +
                                         $"{dataPoint.daynormalizedNbTweets:F6}\t" +
                                         $"{dataPoint.daynormalizedFollowersCount:F6}");
                }

                AppLogger.Log($"GhostNet normalized data dumped to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Failed to dump GhostNet normalized data: {ex.Message}");
            }
        }

        /// <summary>
        ///     Get normalized duration for a given time span
        /// </summary>
        public float GetNormalizedDuration(TimeSpan duration)
        {
            if (!IsDataLoaded) return 0;
            return (float)(duration.TotalSeconds / (_dataBounds.Max.date - _dataBounds.Min.date).TotalSeconds);
        }
    }
}