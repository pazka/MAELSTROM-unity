using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    /**
    "dateday","screen_name","nb_tweets","followers_count"
"2023-02-07","##OTHERS##","11966",9916
"2023-02-07","RER_A","130",257043
"2023-02-07","RERB","115",151203
"2023-02-07","BFMTV","54",4547979
"2023-02-07","Ligne13_RATP","49",58633
"2023-02-07","ClientsRATP","32",68539
"2023-02-07","BFMParis","22",118137
"2023-02-07","Ligne4_RATP","21",46979
*/
    public struct GhostNetDataPoint
    {
        public DateTime date;
        public string screen_name;
        public int nb_tweets;
        public int followers_count;
        public float normalizedNbTweets;
        public float normalizedFollowersCount;
        public float normalizedDate;
        public float dayNormPos; // normalized positive sentiment
        public float dayNormNeu; // normalized neutral sentiment  
        public float dayNormNeg; // normalized negative sentiment
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

                GhostNetDataPoint dataPoint = new GhostNetDataPoint
                {
                    date = date,
                    screen_name = screenName,
                    nb_tweets = nbTweets,
                    followers_count = followersCount,
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
            // Pre-calculate global ranges for efficiency
            float tweetsRange = _dataBounds.Max.nb_tweets - _dataBounds.Min.nb_tweets;
            float followersRange = _dataBounds.Max.followers_count - _dataBounds.Min.followers_count;
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

                // Calculate min/max ratios for this day only once
                float minRatio = float.MaxValue;
                float maxRatio = float.MinValue;

                for (int j = dayStartIndex; j < dayEndIndex; j++)
                {
                    float ratio = _data[j].followers_count > 0 ?
                        (float)_data[j].nb_tweets / _data[j].followers_count : 0;

                    if (ratio < minRatio) minRatio = ratio;
                    if (ratio > maxRatio) maxRatio = ratio;
                }

                // Normalize all data points for this day
                for (int j = dayStartIndex; j < dayEndIndex; j++)
                {
                    // Calculate sentiment values based on tweet count and followers
                    float tweetToFollowerRatio = _data[j].followers_count > 0 ?
                        (float)_data[j].nb_tweets / _data[j].followers_count : 0;

                    float normalizedRatio = maxRatio > minRatio ?
                        (tweetToFollowerRatio - minRatio) / (maxRatio - minRatio) : 0.5f;

                    // Assign sentiment values
                    if (normalizedRatio < 0.3f)
                    {
                        _data[j].dayNormPos = 1.0f - normalizedRatio / 0.3f;
                        _data[j].dayNormNeu = normalizedRatio / 0.3f;
                        _data[j].dayNormNeg = 0.0f;
                    }
                    else if (normalizedRatio < 0.7f)
                    {
                        _data[j].dayNormPos = 0.0f;
                        _data[j].dayNormNeu = 1.0f;
                        _data[j].dayNormNeg = 0.0f;
                    }
                    else
                    {
                        _data[j].dayNormPos = 0.0f;
                        _data[j].dayNormNeu = (1.0f - normalizedRatio) / 0.3f;
                        _data[j].dayNormNeg = (normalizedRatio - 0.7f) / 0.3f;
                    }

                    // Global normalization
                    _data[j].normalizedNbTweets = tweetsRange > 0 ?
                        (float)(_data[j].nb_tweets - _dataBounds.Min.nb_tweets) / tweetsRange : 0;
                    _data[j].normalizedFollowersCount = followersRange > 0 ?
                        (float)(_data[j].followers_count - _dataBounds.Min.followers_count) / followersRange : 0;
                    _data[j].normalizedDate = (float)((_data[j].date.Ticks - _dataBounds.Min.date.Ticks) / dateRange);
                }
            }

            Debug.Log("Data normalized with sentiment values (optimized)");
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
