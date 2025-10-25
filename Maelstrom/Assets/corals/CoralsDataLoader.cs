using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Maelstrom.Unity
{
    public struct CoralDataPoint
    {
        public DateTime date;
        public float normalizedDate;
        public float pos;
        public float neu;
        public float neg;
        public float dayNormPos;
        public float dayNormNeu;
        public float dayNormNeg;
        public float normalizedPos;
        public float normalizedNeu;
        public float normalizedNeg;
    }
    public struct CoralDataBound
    {
        public CoralDataPoint Min;
        public CoralDataPoint Max;
    }

    public class CoralsDataLoader : MonoBehaviour
    {
        [Header("Data Settings")]
        [SerializeField] private TextAsset csvFile;
        private CoralDataPoint[] _data;
        private CoralDataBound _dataBounds;
        private bool _dataLoaded = false;

        public CoralDataPoint[] Data => _data;
        public CoralDataBound DataBounds => _dataBounds;
        public bool IsDataLoaded => _dataLoaded;

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "CoralsScene")
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
            List<CoralDataPoint> dataList = new List<CoralDataPoint>();

            // Skip header line
            bool firstDataPoint = true;

            for (int i = 1; i < lines.Length; i++) // Skip header
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] fields = line.Split(',');
                if (fields.Length < 4) continue;

                // Parse fields: pos, neu, neg, date
                if (!float.TryParse(fields[0].Trim('"'), out float pos)) continue;
                if (!float.TryParse(fields[1].Trim('"'), out float neu)) continue;
                if (!float.TryParse(fields[2].Trim('"'), out float neg)) continue;

                string dateString = fields[3].Trim('"');
                if (!DateTime.TryParse(dateString, out DateTime date)) continue;

                CoralDataPoint dataPoint = new CoralDataPoint
                {
                    date = date,
                    pos = pos,
                    neu = neu,
                    neg = neg
                };

                if (firstDataPoint)
                {
                    _dataBounds.Min = dataPoint;
                    _dataBounds.Max = dataPoint;
                    firstDataPoint = false;
                }
                else
                {
                    if (dataPoint.pos < _dataBounds.Min.pos) _dataBounds.Min.pos = dataPoint.pos;
                    if (dataPoint.pos > _dataBounds.Max.pos) _dataBounds.Max.pos = dataPoint.pos;

                    if (dataPoint.neu < _dataBounds.Min.neu) _dataBounds.Min.neu = dataPoint.neu;
                    if (dataPoint.neu > _dataBounds.Max.neu) _dataBounds.Max.neu = dataPoint.neu;

                    if (dataPoint.neg < _dataBounds.Min.neg) _dataBounds.Min.neg = dataPoint.neg;
                    if (dataPoint.neg > _dataBounds.Max.neg) _dataBounds.Max.neg = dataPoint.neg;

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
            DumpNormalizedDataToCSV();
            _dataLoaded = true;

            Debug.Log($"Data Loaded: {_data.Length} data points");
            Debug.Log($"Data bounds: {_dataBounds.Min.date:yyyy-MM-dd} to {_dataBounds.Max.date:yyyy-MM-dd}");
            Debug.Log($"Pos bounds: {_dataBounds.Min.pos} to {_dataBounds.Max.pos}");
            Debug.Log($"Neu bounds: {_dataBounds.Min.neu} to {_dataBounds.Max.neu}");
            Debug.Log($"Neg bounds: {_dataBounds.Min.neg} to {_dataBounds.Max.neg}");
        }

        private void NormalizeData()
        {
            // Pre-calculate logarithmic ranges for efficiency
            float logMinPos = (float)Math.Log(_dataBounds.Min.pos + 1);
            float logMaxPos = (float)Math.Log(_dataBounds.Max.pos + 1);
            float logMinNeu = (float)Math.Log(_dataBounds.Min.neu + 1);
            float logMaxNeu = (float)Math.Log(_dataBounds.Max.neu + 1);
            float logMinNeg = (float)Math.Log(_dataBounds.Min.neg + 1);
            float logMaxNeg = (float)Math.Log(_dataBounds.Max.neg + 1);
            float dateRange = _dataBounds.Max.date.Ticks - _dataBounds.Min.date.Ticks;

            for (int i = 0; i < _data.Length; i++)
            {
                // Calculate dayNormalize (relative to each other for each day) using logarithmic scaling
                float logPos = (float)Math.Log(_data[i].pos + 1);
                float logNeu = (float)Math.Log(_data[i].neu + 1);
                float logNeg = (float)Math.Log(_data[i].neg + 1);
                
                float maxDayFeeling = Math.Max(logPos, Math.Max(logNeu, logNeg));
                float minDayFeeling = Math.Min(logPos, Math.Min(logNeu, logNeg));

                _data[i].dayNormPos = maxDayFeeling > minDayFeeling ? 
                    (logPos - minDayFeeling) / (maxDayFeeling - minDayFeeling) : 0;
                _data[i].dayNormNeu = maxDayFeeling > minDayFeeling ? 
                    (logNeu - minDayFeeling) / (maxDayFeeling - minDayFeeling) : 0;
                _data[i].dayNormNeg = maxDayFeeling > minDayFeeling ? 
                    (logNeg - minDayFeeling) / (maxDayFeeling - minDayFeeling) : 0;

                // Logarithmic normalization for the entire data set
                _data[i].normalizedPos = logMaxPos > logMinPos ? 
                    (logPos - logMinPos) / (logMaxPos - logMinPos) : 0;
                _data[i].normalizedNeu = logMaxNeu > logMinNeu ? 
                    (logNeu - logMinNeu) / (logMaxNeu - logMinNeu) : 0;
                _data[i].normalizedNeg = logMaxNeg > logMinNeg ? 
                    (logNeg - logMinNeg) / (logMaxNeg - logMinNeg) : 0;
                
                // Clamp normalized values to prevent values > 1.0
                if (_data[i].normalizedPos > 1.0f) _data[i].normalizedPos = 1.0f;
                if (_data[i].normalizedNeu > 1.0f) _data[i].normalizedNeu = 1.0f;
                if (_data[i].normalizedNeg > 1.0f) _data[i].normalizedNeg = 1.0f;

                // Linear normalization for time (as requested)
                _data[i].normalizedDate = (float)((_data[i].date.Ticks - _dataBounds.Min.date.Ticks) / dateRange);
            }

            Debug.Log("Data normalized with logarithmic scaling");
        }

        /// <summary>
        /// Dump normalized data to CSV file for analysis
        /// </summary>
        private void DumpNormalizedDataToCSV()
        {
            try
            {
                string fileName = $"corals_normalized_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string filePath = Path.Combine(Application.dataPath, "..", fileName);
                
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date,real_date,pos,neu,neg,dayNormPos,dayNormNeu,dayNormNeg," +
                                   "normalizedPos,normalizedNeu,normalizedNeg,normalizedDate");
                    
                    // Write data
                    foreach (var dataPoint in _data)
                    {
                        writer.WriteLine($"{dataPoint.date:yyyy-MM-dd HH:mm:ss}," +
                                       $"{dataPoint.date:yyyy-MM-dd HH:mm:ss}," +
                                       $"{dataPoint.pos:F6}," +
                                       $"{dataPoint.neu:F6}," +
                                       $"{dataPoint.neg:F6}," +
                                       $"{dataPoint.dayNormPos:F6}," +
                                       $"{dataPoint.dayNormNeu:F6}," +
                                       $"{dataPoint.dayNormNeg:F6}," +
                                       $"{dataPoint.normalizedPos:F6}," +
                                       $"{dataPoint.normalizedNeu:F6}," +
                                       $"{dataPoint.normalizedNeg:F6}," +
                                       $"{dataPoint.normalizedDate:F6}");
                    }
                }
                
                Debug.Log($"Corals normalized data dumped to: {filePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to dump Corals normalized data: {ex.Message}");
            }
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
