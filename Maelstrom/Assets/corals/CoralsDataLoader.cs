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
            _dataLoaded = true;

            Debug.Log($"Data Loaded: {_data.Length} data points");
            Debug.Log($"Data bounds: {_dataBounds.Min.date:yyyy-MM-dd} to {_dataBounds.Max.date:yyyy-MM-dd}");
            Debug.Log($"Pos bounds: {_dataBounds.Min.pos} to {_dataBounds.Max.pos}");
            Debug.Log($"Neu bounds: {_dataBounds.Min.neu} to {_dataBounds.Max.neu}");
            Debug.Log($"Neg bounds: {_dataBounds.Min.neg} to {_dataBounds.Max.neg}");
        }

        private void NormalizeData()
        {
            for (int i = 0; i < _data.Length; i++)
            {
                // Calculate dayNormalize (relative to each other for each day)
                float maxDayFeeling = Math.Max(_data[i].pos, Math.Max(_data[i].neu, _data[i].neg));
                float minDayFeeling = Math.Min(_data[i].pos, Math.Min(_data[i].neu, _data[i].neg));

                _data[i].dayNormPos = (_data[i].pos - minDayFeeling) / (maxDayFeeling - minDayFeeling);
                _data[i].dayNormNeu = (_data[i].neu - minDayFeeling) / (maxDayFeeling - minDayFeeling);
                _data[i].dayNormNeg = (_data[i].neg - minDayFeeling) / (maxDayFeeling - minDayFeeling);

                float posRange = _dataBounds.Max.pos - _dataBounds.Min.pos;
                float neuRange = _dataBounds.Max.neu - _dataBounds.Min.neu;
                float negRange = _dataBounds.Max.neg - _dataBounds.Min.neg;
                float dateRange = _dataBounds.Max.date.Ticks - _dataBounds.Min.date.Ticks;

                _data[i].normalizedPos = posRange > 0 ? (_data[i].pos - _dataBounds.Min.pos) / posRange : 0;
                _data[i].normalizedNeu = neuRange > 0 ? (_data[i].neu - _dataBounds.Min.neu) / neuRange : 0;
                _data[i].normalizedNeg = negRange > 0 ? (_data[i].neg - _dataBounds.Min.neg) / negRange : 0;
                _data[i].normalizedDate = (float)((_data[i].date.Ticks - _dataBounds.Min.date.Ticks) / dateRange);
            }

            Debug.Log("Data normalized");
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
