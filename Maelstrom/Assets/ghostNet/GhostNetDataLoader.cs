using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    /**"dateday","screen_name","nb_tweets","avg_followers_count","nb_account_if_others"
"2023-02-07","##OTHERS##","11966","999","8696"
"2023-02-07","RER_A","130","257043","1"
"2023-02-07","RERB","115","151203","1"
"2023-02-07","BFMTV","54","4547979","1"
"2023-02-07","Ligne13_RATP","49","58633","1"
"2023-02-07","ClientsRATP","32","68539","1"
"2023-02-07","BFMParis","22","118137","1"
"2023-02-07","Ligne4_RATP","21","46979","1"
"2023-02-07","Ligne8_RATP","17","53259","1"
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
        [Header("Data Settings")]
        [SerializeField] private TextAsset csvFile;
        private GhostNetDataPoint[] _data;
        private GhostNetDataBound _dataBounds;
        private bool _dataLoaded = false;

        public GhostNetDataPoint[] Data => _data;
        public GhostNetDataBound DataBounds => _dataBounds;
        public bool IsDataLoaded => _dataLoaded;

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "GhostNetsScene")
            {
                return;
            }

            LoadData();
        }

        /// <summary>
        /// Load data from CSV file
        /// </summary>
        public void LoadData()
        {
            if (csvFile == null)
            {
                Debug.LogError("CSV file is not assigned!");
                return;
            }
            Debug.Log("Loading ghostNetData");

            string[] lines = csvFile.text.Split('\n');
            List<GhostNetDataPoint> dataList = new List<GhostNetDataPoint>();

            // Skip header line
            bool firstDataPoint = true;

            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] fields = line.Split(',');
                if (fields.Length < 4) continue;

                // Parse fields: dateday, screen_name, nb_tweets, followers_count
                string dateString = fields[0].Trim('"');
                if (!DateTime.TryParse(dateString, out DateTime date)) continue;

                string screenName = fields[1].Trim('"');

                if (!int.TryParse(fields[2].Trim('"'), out int nbTweets)) continue;
                if (!int.TryParse(fields[3].Trim('"'), out int followersCount)) continue;
                if (!int.TryParse(fields[4].Trim('"'), out int nbAccountsOthers)) continue;

                GhostNetDataPoint dataPoint = new GhostNetDataPoint
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
                    if (dataPoint.nb_tweets < _dataBounds.Min.nb_tweets) _dataBounds.Min.nb_tweets = dataPoint.nb_tweets;
                    if (dataPoint.nb_tweets > _dataBounds.Max.nb_tweets) _dataBounds.Max.nb_tweets = dataPoint.nb_tweets;

                    if (dataPoint.followers_count < _dataBounds.Min.followers_count) _dataBounds.Min.followers_count = dataPoint.followers_count;
                    if (dataPoint.followers_count > _dataBounds.Max.followers_count) _dataBounds.Max.followers_count = dataPoint.followers_count;

                    if (dataPoint.date < _dataBounds.Min.date) _dataBounds.Min.date = dataPoint.date;
                    if (dataPoint.date > _dataBounds.Max.date) _dataBounds.Max.date = dataPoint.date;
                }

                // Check if data is in chronological order
                if (dataList.Count > 0 && dataPoint.date < dataList[dataList.Count - 1].date)
                {
                    throw new System.Exception("Data is not in chronological order");
                }

                // For regular accounts, add as-is
                dataList.Add(dataPoint);
            }

            _data = dataList.ToArray();
            NormalizeData();
            _dataLoaded = true;

            Debug.Log($"Data Loaded: {_data.Length} data points");
            Debug.Log($"Data bounds: {_dataBounds.Min.date:yyyy-MM-dd} to {_dataBounds.Max.date:yyyy-MM-dd}");
            Debug.Log($"Tweets bounds: {_dataBounds.Min.nb_tweets} to {_dataBounds.Max.nb_tweets}");
            Debug.Log($"Followers bounds: {_dataBounds.Min.followers_count} to {_dataBounds.Max.followers_count}");
        }

        private void NormalizeData()
        {
            // Detect and cap outliers in followers count to prevent normalization skewing
            float cappedMaxFollowers = _dataBounds.Max.followers_count;
            float outlierThreshold = _dataBounds.Max.followers_count * 0.1f; // 10% of max
            
            // Find the second highest followers count to use as cap if max is an outlier
            float secondHighestFollowers = 0f;
            foreach (var dataPoint in _data)
            {
                if (dataPoint.followers_count > secondHighestFollowers && 
                    dataPoint.followers_count < _dataBounds.Max.followers_count)
                {
                    secondHighestFollowers = dataPoint.followers_count;
                }
            }
            
            // If the max is significantly larger than the second highest, cap it
            if (secondHighestFollowers > 0 && _dataBounds.Max.followers_count > secondHighestFollowers * 10f)
            {
                cappedMaxFollowers = secondHighestFollowers;
                Debug.Log($"Capped followers normalization: Original max {_dataBounds.Max.followers_count:N0} -> Capped max {cappedMaxFollowers:N0} (outlier detected)");
            }

            // Pre-calculate global ranges for efficiency
            float tweetsRange = _dataBounds.Max.nb_tweets - _dataBounds.Min.nb_tweets;
            float followersRange = cappedMaxFollowers - _dataBounds.Min.followers_count;
            float dateRange = _dataBounds.Max.date.Ticks - _dataBounds.Min.date.Ticks;

            // Process data in chronological order, grouping by date
            int i = 0;
            while (i < _data.Length)
            {
                DateTime currentDate = _data[i].date.Date;
                int dayStartIndex = i;

                // Find all data points for the same day
                while (i < _data.Length && _data[i].date.Date == currentDate)
                {
                    i++;
                }
                int dayEndIndex = i;

                // Calculate min/max teets count and follower count for this day only once
                float maxDayTweets = 0;
                float minDayTweets = 0;
                float maxDayFollowers = 0;
                float minDayFollowers = 0;
                for (int j = dayStartIndex; j < dayEndIndex; j++)
                {
                    if (_data[j].nb_tweets > maxDayTweets) maxDayTweets = _data[j].nb_tweets;
                    if (_data[j].nb_tweets < minDayTweets) minDayTweets = _data[j].nb_tweets;
                    if (_data[j].followers_count > maxDayFollowers) maxDayFollowers = _data[j].followers_count;
                    if (_data[j].followers_count < minDayFollowers) minDayFollowers = _data[j].followers_count;
                }

                // Normalize all data points for this day
                for (int j = dayStartIndex; j < dayEndIndex; j++)
                {
                    if(_data[j].screen_name == "##OTHERS##")
                    {
                        continue;
                    }
                    
                    //normalization for the day
                    _data[j].daynormalizedNbTweets = maxDayTweets > 0 ?
                        (float)(_data[j].nb_tweets - minDayTweets) / (maxDayTweets - minDayTweets) : 0;
                    _data[j].daynormalizedFollowersCount = maxDayFollowers > 0 ?
                        (float)(_data[j].followers_count - minDayFollowers) / (maxDayFollowers - minDayFollowers) : 0;

                    //  normalization given the entire data set
                    _data[j].normalizedNbTweets = tweetsRange > 0 ?
                        (float)(_data[j].nb_tweets - _dataBounds.Min.nb_tweets) / tweetsRange : 0;
                    _data[j].normalizedFollowersCount = followersRange > 0 ?
                        (float)(_data[j].followers_count - _dataBounds.Min.followers_count) / followersRange : 0;
                    
                    // Clamp normalized followers count to prevent values > 1.0 when using capped max
                    if (_data[j].normalizedFollowersCount > 1.0f)
                    {
                        _data[j].normalizedFollowersCount = 1.0f;
                    }

                    _data[j].normalizedDate = (float)((_data[j].date.Ticks - _dataBounds.Min.date.Ticks) / dateRange);
                }
            }

            Debug.Log($"Data normalized with outlier capping (optimized) - Followers range: {_dataBounds.Min.followers_count:N0} to {cappedMaxFollowers:N0}");
        }

        /// <summary>
        /// Get normalized duration for a given time span
        /// </summary>
        public float GetNormalizedDuration(TimeSpan duration)
        {
            if (!_dataLoaded) return 0;
            return (float)(duration.TotalSeconds / (_dataBounds.Max.date - _dataBounds.Min.date).TotalSeconds);
        }
    }
}
