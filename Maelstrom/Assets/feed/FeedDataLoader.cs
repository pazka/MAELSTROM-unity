using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    [System.Serializable]
    public struct FeedDataPoint
    {
        public DateTime date;
        public int retweetCount;
        public float normalizedRetweetCount;
        public float normalizedDate;
    }

    [System.Serializable]
    public struct FeedDataBound
    {
        public FeedDataPoint Min;
        public FeedDataPoint Max;
    }

    /// <summary>
    /// Loads and manages CSV data for the Feed visualization
    /// </summary>
    public class FeedDataLoader : MonoBehaviour
    {
        [Header("Data Settings")]
        [SerializeField] private TextAsset csvFile;
        private FeedDataPoint[] _data;
        private FeedDataBound _dataBounds;
        private bool _dataLoaded = false;

        public FeedDataPoint[] Data => _data;
        public FeedDataBound DataBounds => _dataBounds;
        public bool IsDataLoaded => _dataLoaded;

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "FeedScene")
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
            List<FeedDataPoint> dataList = new List<FeedDataPoint>();

            // Skip header line
            bool firstDataPoint = true;

            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] fields = line.Split(',');
                if (fields.Length < 2) continue;

                // Remove quotes from date field if present
                string dateString = fields[0].Trim('"');
                if (!DateTime.TryParse(dateString, out DateTime date)) continue;

                // Remove quotes from retweet count field if present
                string retweetString = fields[1].Trim('"');
                if (!int.TryParse(retweetString, out int retweetCount)) continue;

                FeedDataPoint dataPoint = new FeedDataPoint
                {
                    date = date,
                    retweetCount = retweetCount
                };

                if (firstDataPoint)
                {
                    _dataBounds.Min = dataPoint;
                    _dataBounds.Max = dataPoint;
                    firstDataPoint = false;
                }
                else
                {
                    if (dataPoint.retweetCount < _dataBounds.Min.retweetCount)
                        _dataBounds.Min.retweetCount = dataPoint.retweetCount;
                    if (dataPoint.retweetCount > _dataBounds.Max.retweetCount)
                        _dataBounds.Max.retweetCount = dataPoint.retweetCount;
                    if (dataPoint.date < _dataBounds.Min.date)
                        _dataBounds.Min.date = dataPoint.date;
                    if (dataPoint.date > _dataBounds.Max.date)
                        _dataBounds.Max.date = dataPoint.date;
                }

                //check if data is indeed later in time than the previous data point
                if (dataList.Count > 0 && dataPoint.date < dataList[i - 2].date)
                {
                    throw new System.Exception("Data is not in chronological order");
                }

                dataList.Add(dataPoint);
            }

            _data = dataList.ToArray();
            NormalizeData();
            DumpNormalizedDataToCSV();
            _dataLoaded = true;

            Debug.Log($"Data Loaded: {_data.Length} data points");
            Debug.Log($"Data bounds: {_dataBounds.Min.date:yyyy-MM-dd HH:mm:ss} to {_dataBounds.Max.date:yyyy-MM-dd HH:mm:ss}");
            Debug.Log($"Retweet bounds: {_dataBounds.Min.retweetCount} to {_dataBounds.Max.retweetCount}");
        }

        private void NormalizeData()
        {
            for (int i = 0; i < _data.Length; i++)
            {
                // Logarithmic normalization for retweet count
                float logMin = (float)Math.Log(_dataBounds.Min.retweetCount + 1);
                float logMax = (float)Math.Log(_dataBounds.Max.retweetCount + 1);
                float logCurrent = (float)Math.Log(_data[i].retweetCount + 1);

                _data[i].normalizedRetweetCount = (logCurrent - logMin) / (logMax - logMin);
                _data[i].normalizedDate = (float)((_data[i].date - _dataBounds.Min.date).TotalSeconds /
                    (_dataBounds.Max.date - _dataBounds.Min.date).TotalSeconds);
            }

            Debug.Log("Data normalized");
        }

        /// <summary>
        /// Dump normalized data to CSV file for analysis
        /// </summary>
        private void DumpNormalizedDataToCSV()
        {
            try
            {
                string fileName = $"feed_normalized_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date;real_date;retweetCount;normalizedRetweetCount;normalizedDate");

                    // Write data
                    foreach (var dataPoint in _data)
                    {
                        writer.WriteLine($"{dataPoint.date:yyyy-MM-dd HH:mm:ss};" +
                                       $"{dataPoint.date:yyyy-MM-dd HH:mm:ss};" +
                                       $"{dataPoint.retweetCount};" +
                                       $"{dataPoint.normalizedRetweetCount:F6};" +
                                       $"{dataPoint.normalizedDate:F6}");
                    }
                }

                Debug.Log($"Feed normalized data dumped to: {filePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to dump Feed normalized data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get normalized duration for a given time span
        /// </summary>
        public float GetNormalizedDuration(TimeSpan duration)
        {
            return (float)(duration.TotalSeconds / (_dataBounds.Max.date - _dataBounds.Min.date).TotalSeconds);
        }
    }
}
