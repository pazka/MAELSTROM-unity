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
        [Header("Data Settings")] [SerializeField]
        private TextAsset csvFile;

        private CoralDataBound _dataBounds;

        public CoralDataPoint[] Data { get; private set; }

        public CoralDataBound DataBounds => _dataBounds;
        public bool IsDataLoaded { get; private set; }

        private void Awake()
        {
            if (SceneManager.GetActiveScene().name != "CoralsScene") return;

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

            var lines = csvFile.text.Split('\n');
            var dataList = new List<CoralDataPoint>();

            // Skip header line
            var firstDataPoint = true;

            for (var i = 1; i < lines.Length; i++) // Skip header
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = line.Split(',');
                if (fields.Length < 4) continue;

                // Parse fields: pos, neu, neg, date
                if (!float.TryParse(fields[0].Trim('"'), out var pos)) continue;
                if (!float.TryParse(fields[1].Trim('"'), out var neu)) continue;
                if (!float.TryParse(fields[2].Trim('"'), out var neg)) continue;

                var dateString = fields[3].Trim('"');
                if (!DateTime.TryParse(dateString, out var date)) continue;

                var dataPoint = new CoralDataPoint
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
                    throw new Exception("Data is not in chronological order");

                dataList.Add(dataPoint);
            }

            Data = dataList.ToArray();
            NormalizeData();
            if (Config.Get("dataDump", false))
                DumpNormalizedDataToCSV();
            IsDataLoaded = true;

            AppLogger.Log($"Data Loaded: {Data.Length} data points");
            AppLogger.Log($"Data bounds: {_dataBounds.Min.date:yyyy-MM-dd} to {_dataBounds.Max.date:yyyy-MM-dd}");
            AppLogger.Log($"Pos bounds: {_dataBounds.Min.pos} to {_dataBounds.Max.pos}");
            AppLogger.Log($"Neu bounds: {_dataBounds.Min.neu} to {_dataBounds.Max.neu}");
            AppLogger.Log($"Neg bounds: {_dataBounds.Min.neg} to {_dataBounds.Max.neg}");
        }

        private void NormalizeData()
        {
            // Pre-calculate logarithmic ranges for efficiency
            var logMinPos = (float)Math.Log(_dataBounds.Min.pos + 1);
            var logMaxPos = (float)Math.Log(_dataBounds.Max.pos + 1);
            var logMinNeu = (float)Math.Log(_dataBounds.Min.neu + 1);
            var logMaxNeu = (float)Math.Log(_dataBounds.Max.neu + 1);
            var logMinNeg = (float)Math.Log(_dataBounds.Min.neg + 1);
            var logMaxNeg = (float)Math.Log(_dataBounds.Max.neg + 1);
            float dateRange = _dataBounds.Max.date.Ticks - _dataBounds.Min.date.Ticks;

            for (var i = 0; i < Data.Length; i++)
            {
                // Calculate dayNormalize (relative to each other for each day) using logarithmic scaling
                var logPos = (float)Math.Log(Data[i].pos + 1);
                var logNeu = (float)Math.Log(Data[i].neu + 1);
                var logNeg = (float)Math.Log(Data[i].neg + 1);

                var maxDayFeeling = Math.Max(logPos, Math.Max(logNeu, logNeg));
                var minDayFeeling = Math.Min(logPos, Math.Min(logNeu, logNeg));

                Data[i].dayNormPos = maxDayFeeling > minDayFeeling
                    ? (logPos - minDayFeeling) / (maxDayFeeling - minDayFeeling)
                    : 0;
                Data[i].dayNormNeu = maxDayFeeling > minDayFeeling
                    ? (logNeu - minDayFeeling) / (maxDayFeeling - minDayFeeling)
                    : 0;
                Data[i].dayNormNeg = maxDayFeeling > minDayFeeling
                    ? (logNeg - minDayFeeling) / (maxDayFeeling - minDayFeeling)
                    : 0;

                // Logarithmic normalization for the entire data set
                Data[i].normalizedPos = logMaxPos > logMinPos ? (logPos - logMinPos) / (logMaxPos - logMinPos) : 0;
                Data[i].normalizedNeu = logMaxNeu > logMinNeu ? (logNeu - logMinNeu) / (logMaxNeu - logMinNeu) : 0;
                Data[i].normalizedNeg = logMaxNeg > logMinNeg ? (logNeg - logMinNeg) / (logMaxNeg - logMinNeg) : 0;

                // Clamp normalized values to prevent values > 1.0
                if (Data[i].normalizedPos > 1.0f) Data[i].normalizedPos = 1.0f;
                if (Data[i].normalizedNeu > 1.0f) Data[i].normalizedNeu = 1.0f;
                if (Data[i].normalizedNeg > 1.0f) Data[i].normalizedNeg = 1.0f;

                // Linear normalization for time (as requested)
                Data[i].normalizedDate = (Data[i].date.Ticks - _dataBounds.Min.date.Ticks) / dateRange;
            }

            AppLogger.Log("Data normalized with logarithmic scaling");
        }

        /// <summary>
        ///     Dump normalized data to CSV file for analysis
        /// </summary>
        private void DumpNormalizedDataToCSV()
        {
            try
            {
                var fileName = $"corals_normalized_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                var filePath = Path.Combine(Application.dataPath, "..", fileName);

                using (var writer = new StreamWriter(filePath))
                {
                    // Write header
                    writer.WriteLine("date;real_date;pos;neu;neg;dayNormPos;dayNormNeu;dayNormNeg;" +
                                     "normalizedPos;normalizedNeu;normalizedNeg;normalizedDate");

                    // Write data
                    foreach (var dataPoint in Data)
                        writer.WriteLine($"{dataPoint.date:yyyy-MM-dd HH:mm:ss};" +
                                         $"{dataPoint.date:yyyy-MM-dd HH:mm:ss};" +
                                         $"{dataPoint.pos:F6};" +
                                         $"{dataPoint.neu:F6};" +
                                         $"{dataPoint.neg:F6};" +
                                         $"{dataPoint.dayNormPos:F6};" +
                                         $"{dataPoint.dayNormNeu:F6};" +
                                         $"{dataPoint.dayNormNeg:F6};" +
                                         $"{dataPoint.normalizedPos:F6};" +
                                         $"{dataPoint.normalizedNeu:F6};" +
                                         $"{dataPoint.normalizedNeg:F6};" +
                                         $"{dataPoint.normalizedDate:F6}");
                }

                AppLogger.Log($"Corals normalized data dumped to: {filePath}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Failed to dump Corals normalized data: {ex.Message}");
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