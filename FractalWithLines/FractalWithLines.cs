using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    /**
     * FullFractal - Version 1.5
     */
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class FractalWithLines : Indicator
    {
        [Parameter("Period", DefaultValue = 5, MinValue = 5)]
        public int period { get; set; }

        [Parameter("Mark fractal value", DefaultValue = true)]
        public bool plotValueMarkers { get; set; }

        [Parameter("Timeframes", DefaultValue = "")]
        public string timeframes { get; set; }


        [Parameter("Mark fakes", DefaultValue = true, Group = "Fakes")]
        public bool markFakes { get; set; }

        [Parameter("Fakes color", DefaultValue = SystemColor.White, Group = "Fakes")]
        public SystemColor fakesColor { get; set; }

        // High-High
        [Parameter("Show High-High line", DefaultValue = true, Group = "High-High")]
        public bool showHorizontalContinuationLine { get; set; }

        [Parameter("High-High line color", DefaultValue = SystemColor.Red, Group = "High-High")]
        public SystemColor horizontalTopLineColor { get; set; }

        // Low-Low
        [Parameter("Show Low-Low line", DefaultValue = true, Group = "Low-Low")]
        public bool showVerticalContinuationLine { get; set; }

        [Parameter("Low-Low line color", DefaultValue = SystemColor.DarkCyan, Group = "Low-Low")]
        public SystemColor horizontalBottomLineColor { get; set; }

        // High-Low
        [Parameter("Show High-Low line", DefaultValue = true, Group = "High-Low")]
        public bool linkHighLow { get; set; }

        [Parameter("Diagonal line color", DefaultValue = SystemColor.Beige, Group = "High-Low")]
        public SystemColor diagonalLineColor { get; set; }


        // Notifications
        [Parameter("Send notification to email", DefaultValue = "", Group = "Notifications")]
        public string emailNewFractalNotificationTo { get; set; }

        [Parameter("Play sound on new High ...\n(i.e: C:\\Windows\\Media\\chimes.wav)", DefaultValue = "", Group = "Notifications")]
        public string newHighSound { get; set; }

        [Parameter("Play sound on new Low ...\n(i.e: C:\\Windows\\Media\\chord.wav)", DefaultValue = "", Group = "Notifications")]
        public string newLowSound { get; set; }

        private const LineStyle linkLineStyle = LineStyle.Lines;
        private const string PREFIX = "fwl--";
        private const string circleIcon = "⊙";
        private const string fakeIcon = "⮾";
        private NotificationManager notificationManager;

        public List<FractalService> fractalServices { get; set; }

        protected override void Initialize()
        {
            notificationManager = new NotificationManager(this, emailNewFractalNotificationTo, newHighSound, newLowSound);
            fractalServices = new List<FractalService>();
            removeAll();
            Print("Initializing Fractal With Lines version 1.5");

            if (timeframes == null || timeframes.Length == 0)
            {
                timeframes = TimeFrame.ShortName;
            }
            var timeframeItems = new List<string>(timeframes.Replace(" ", "").Split(","));
            Print("Timeframe items: " + String.Join(", ", timeframeItems));
            foreach (var timeframeName in timeframeItems)
            {
                Print("Building service for timeframe " + timeframeName);
                FractalOptions options = new FractalOptions(period, showHorizontalContinuationLine, showVerticalContinuationLine, linkHighLow);
                TimeFrame timeFrame = TimeframeTranslator.translate(timeframeName);
                Print("Timeframe found: " + timeFrame.Name);
                var bars = MarketData.GetBars(timeFrame);

                int lastCount = 0;
                var initialTime = Chart.Bars[0].OpenTime;
                while (bars[0].OpenTime.CompareTo(initialTime) > 0 && bars.Count != lastCount)
                {
                    Print("Load more history for {0} ({1}). Total: {2}", Symbol.Name, timeFrame.Name, lastCount);
                    lastCount = bars.Count;
                    bars.LoadMoreHistory();
                }

                var fractalService = new FractalService(bars, options);
                fractalServices.Add(fractalService);

                fractalService.onFractal((e) => plot(e, bars));
                bars.BarOpened += (BarOpenedEventArgs e) => this.HandleBarOpened(e.Bars.Count - 1, fractalService);
                bars.Reloaded += (BarsHistoryLoadedEventArgs e) => this.handleReload(e, fractalService);
                for (int i = 0; i < bars.Count; i++)
                {
                    var bar = bars[i];
                    this.HandleBarOpened(i, fractalService);
                }
            }
        }

        private void handleReload(BarsHistoryLoadedEventArgs e, FractalService fractalService)
        {
            Print("Reloading " + fractalService.id);
            List<String> namesToRemove = Chart.Objects.Select(o =>
            {
                if (o.Name != null && o.Name.StartsWith(PREFIX + fractalService.id + "-"))
                {
                    return o.Name;
                }
                return null;
            })
                .Where(v => v != null)
                .ToList();

            namesToRemove.ForEach(n => Chart.RemoveObject(n));
        }

        private void removeAll()
        {
            Chart.Objects.Select(o =>
            {
                if (o.Name != null && o.Name.StartsWith(PREFIX))
                {
                    return o.Name;
                }
                return null;
            })
                .Where(v => v != null)
                .ToList()
                .ForEach(n => Chart.RemoveObject(n));
        }

        private void HandleBarOpened(int index, FractalService fractalService)
        {
            int effectiveIndex = index - 1;
            fractalService.processIndex(effectiveIndex);

            plotHorizontalContinuationLine(index, fractalService.getLastHighFractal(), fractalService);
            plotHorizontalContinuationLine(index, fractalService.getLastLowFractal(), fractalService);
        }

        private void plot(FractalEvent fractalEvent, Bars bars)
        {
            Fractal fractal = fractalEvent.fractal;
            plotHighLowLine(fractal.getPrevious(), fractal.getBest());
            if (markFakes)
            {
                plotBadFractalSignals(fractal, bars);
            }
            if (plotValueMarkers)
            {
                plotFractalMarker(fractal, bars);
            }
            plotVerticalContinuationLine(fractal.getBest());
            notificationManager.sendEmailNotification(fractal);
            notificationManager.playSoundNotification(fractal);
        }

        private void plotVerticalContinuationLine(Fractal fractal)
        {
            if (!showVerticalContinuationLine)
                return;

            Fractal previousSameSideFractal = fractal.getPreviousOfSameSide();
            if (previousSameSideFractal == null)
                return;

            Color color = fractal.high ? horizontalTopLineColor.ToCtraderColor() : horizontalBottomLineColor.ToCtraderColor();
            String name = getPrefix(previousSameSideFractal) + previousSameSideFractal.index + "-vertical-line-" + (fractal.high ? "high" : "low");
            Chart.DrawTrendLine(name, fractal.dateTime, previousSameSideFractal.value, fractal.dateTime, fractal.value, color, 1, LineStyle.Dots);
        }

        private static string getPrefix(Fractal fractal)
        {
            return PREFIX + fractal.prefix + "-";
        }

        private void plotHorizontalContinuationLine(int index, Fractal fractal, FractalService fractalService)
        {
            if (!showHorizontalContinuationLine || fractal == null)
                return;

            int middleIndex = fractalService.getMiddleIndex(index - 1);
            bool isNewFractal = middleIndex == fractal.index;
            if (isNewFractal)
                plotHighHighLineForPreviousFractalOfSameSide(fractal, middleIndex, fractalService.bars);

            int lastOpositeFractalIndex = getPreviousIndex(fractal);

            var previousFractal = fractal.getPreviousOfSameSide();
            if (previousFractal != null)
            {
                var prevIndex = getPreviousIndex(previousFractal);
                var prevName = getPrefix(previousFractal) + (previousFractal.high ? "high" : "low") + "-horizontal-line-" + prevIndex;
                plotHorizontalLine(fractal.index, previousFractal, prevName, fractalService.bars);
            }
            var name = getPrefix(fractal) + (fractal.high ? "high" : "low") + "-horizontal-line-" + lastOpositeFractalIndex;
            plotHorizontalLine(index, fractal, name, fractalService.bars);
        }

        private void plotHighHighLineForPreviousFractalOfSameSide(Fractal fractal, int middleIndex, Bars bars)
        {
            Fractal previousOfSameSide = fractal.getPreviousOfSameSide();
            if (previousOfSameSide == null)
                return;
            int previousIndex = getPreviousIndex(previousOfSameSide);
            String newLineName = (fractal.high ? "high" : "low") + "-horizontal-line-" + previousIndex;
            plotHorizontalLine(middleIndex, previousOfSameSide, newLineName, bars);
        }

        private void plotHorizontalLine(int index, Fractal fractal, string name, Bars bars)
        {
            Color color = fractal.high ? horizontalTopLineColor.ToCtraderColor() : horizontalBottomLineColor.ToCtraderColor();
            Chart.DrawTrendLine(name, fractal.dateTime, fractal.value, bars[index].OpenTime, fractal.value, color, 1, LineStyle.Dots);
        }

        private void plotHighLowLine(Fractal fractal1, Fractal fractal2)
        {
            if (!linkHighLow || fractal1 == null || fractal2 == null)
                return;

            Chart.DrawTrendLine(getPrefix(fractal1) + fractal1.dateTime.ToString() + "-link",
                fractal1.dateTime, fractal1.value, fractal2.dateTime, fractal2.value, diagonalLineColor.ToCtraderColor(), 1, linkLineStyle);
        }

        private void plotBadFractalSignals(Fractal fractal, Bars bars)
        {
            List<Fractal> allWorse = fractal.getBadFractals();
            for (int i = 0; i < allWorse.Count; i++)
            {
                var badFractal = allWorse[i];
                Chart.RemoveObject(getPrefix(badFractal) + "-" + badFractal.index + "-marker-" + (badFractal.high ? "high" : "low"));
                plotBadFractalMarker(getPrefix(fractal) + getPreviousIndex(fractal) + "-badSignal-" + i, badFractal);
            }
        }

        private void plotBadFractalMarker(string name, Fractal fractal)
        {
            var t = Chart.DrawText(name, fakeIcon, fractal.dateTime, fractal.value, fakesColor.ToCtraderColor());
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
        }

        private void plotFractalMarker(Fractal fractal, Bars bars)
        {
            var name = getPrefix(fractal) + "-" + fractal.index + "-marker-" + (fractal.high ? "high" : "low");
            Color color = getMarkerColor(fractal);
            var t = Chart.DrawText(name, circleIcon, fractal.dateTime, fractal.value, color);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Center;
        }

        private static int getPreviousIndex(Fractal fractal)
        {
            Fractal previous = fractal == null ? null : fractal.getPrevious();
            int previousIndex = previous == null ? 0 : previous.index;
            return previousIndex;
        }

        private static Color getMarkerColor(Fractal fractal)
        {
            switch (fractal.getFractalType())
            {
                case FractalType.HigherHigh:
                    return Color.Blue;
                case FractalType.LowerHigh:
                    return Color.DarkCyan;
                case FractalType.HigherLow:
                    return Color.Pink;
                case FractalType.LowerLow:
                    return Color.Red;
            }
            return Color.White;
        }


        public override void Calculate(int index)
        {
        }
    }
}