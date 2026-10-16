using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace OpenSstpWin
{
    internal readonly record struct TrafficSample(long Rx, long Tx);

    /// <summary>
    /// Live statistics of the VPN adapter. All calls come from the UI thread.
    /// Values are computed from the real byte counters of the Windows network interface.
    /// </summary>
    internal sealed class TrafficMonitor
    {
        internal const int MaxSamples = 1800; // 30 minutes at 1 sample per second

        private readonly List<TrafficSample> samples = new List<TrafficSample>();
        private readonly Stopwatch clock = new Stopwatch();

        private long baseRx, baseTx, lastRx, lastTx, lastMs;

        internal bool Active { get; private set; }
        internal long RxSpeed { get; private set; }
        internal long TxSpeed { get; private set; }
        internal long TotalRx { get; private set; }
        internal long TotalTx { get; private set; }
        internal IReadOnlyList<TrafficSample> Samples => samples;

        internal void Start(long rx, long tx)
        {
            samples.Clear();
            baseRx = lastRx = rx;
            baseTx = lastTx = tx;
            RxSpeed = TxSpeed = TotalRx = TotalTx = 0;
            lastMs = 0;
            clock.Restart();
            Active = true;
        }

        internal void Stop()
        {
            Active = false;
            samples.Clear();
            RxSpeed = TxSpeed = TotalRx = TotalTx = 0;
            clock.Reset();
        }

        internal void Sample(long rx, long tx)
        {
            if (!Active) return;

            long now = clock.ElapsedMilliseconds;
            long elapsed = Math.Max(now - lastMs, 1L);

            RxSpeed = Math.Max(0L, rx - lastRx) * 1000L / elapsed;
            TxSpeed = Math.Max(0L, tx - lastTx) * 1000L / elapsed;
            TotalRx = Math.Max(0L, rx - baseRx);
            TotalTx = Math.Max(0L, tx - baseTx);

            lastRx = rx;
            lastTx = tx;
            lastMs = now;

            samples.Add(new TrafficSample(RxSpeed, TxSpeed));
            while (samples.Count > MaxSamples) samples.RemoveAt(0);
        }
    }
}
