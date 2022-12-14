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
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class FractalWithLines : Indicator
    {
        [Parameter(DefaultValue = 5, MinValue = 5)]
        public int period { get; set; }

        [Parameter("Horizontal Continuation line", DefaultValue = true)]
        public bool showHorizontalContinuationLine { get; set; }

        [Parameter("Vertical Continuation line", DefaultValue = true)]
        public bool showVerticalContinuationLine { get; set; }

        [Parameter("Link highs and lows", DefaultValue = true)]
        public bool linkHighLow { get; set; }

        [Parameter("Plot arrows", DefaultValue = true)]
        public bool plotArrows { get; set; }

        [Parameter("Timeframes", DefaultValue = "")]
        public String timeframes { get; set; }

        [Parameter("Mark fakes", DefaultValue = true)]
        public bool markFakes { get; set; }

        [Parameter("Top line color", DefaultValue = SystemColor.Red)]
        public SystemColor horizontalTopLineColor { get; set; }

        [Parameter("Bottom line color", DefaultValue = SystemColor.DarkCyan)]
        public SystemColor horizontalBottomLineColor { get; set; }

        [Parameter("Diagonal line color", DefaultValue = SystemColor.Beige)]
        public SystemColor diagonalLineColor { get; set; }

        private const LineStyle linkLineStyle = LineStyle.Lines;
        private const string PREFIX = "fwl--";

        protected override void Initialize()
        {
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

                fractalService.onFractal((e) => plot(e, bars));
                bars.BarOpened += (BarOpenedEventArgs e) => this.HandleBarOpened(e.Bars.Count - 1, fractalService, e.Bars);
                bars.Reloaded += (BarsHistoryLoadedEventArgs e) => this.handleReload(e, fractalService);
                for (int i = 0; i < bars.Count; i++)
                {
                    var bar = bars[i];
                    this.HandleBarOpened(i, fractalService, bars);
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

        private void HandleBarOpened(int index, FractalService fractalService, Bars bars)
        {
            int effectiveIndex = index - 1;
            //Print("Handling bar at " + bars[index].OpenTime.ToString());
            fractalService.processIndex(effectiveIndex);

            plotHorizontalContinuationLine(index, fractalService.getLastHighFractal(), fractalService, bars);
            plotHorizontalContinuationLine(index, fractalService.getLastLowFractal(), fractalService, bars);
        }

        public override void Calculate(int index)
        {
        }


        private void plot(FractalEvent fractalEvent, Bars bars)
        {
            Fractal fractal = fractalEvent.fractal;
            plotFractalsLink(fractal.getPrevious(), fractal.getBest());
            if (markFakes)
            {
                plotBadFractalSignals(fractal, bars);
            }
            if (plotArrows)
            {
                plotArrow(fractal, bars);
            }
            plotVerticalContinuationLine(fractal.getBest());
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

        private void plotHorizontalContinuationLine(int index, Fractal fractal, FractalService fractalService, Bars bars)
        {
            if (!showHorizontalContinuationLine || fractal == null)
                return;

            int middleIndex = fractalService.getMiddleIndex(index - 1);
            bool isNewFractal = middleIndex == fractal.index;
            if (isNewFractal)
                drawHorizontalLineForPreviousFractalOfSameSide(fractal, middleIndex, bars);

            int lastOpositeFractalIndex = getPreviousIndex(fractal);

            var previousFractal = fractal.getPreviousOfSameSide();
            if (previousFractal != null)
            {
                var prevIndex = getPreviousIndex(previousFractal);
                var prevName = getPrefix(previousFractal) + (previousFractal.high ? "high" : "low") + "-horizontal-line-" + prevIndex;
                drawHorizontalLine(fractal.index, previousFractal, prevName, bars);
            }
            var name = getPrefix(fractal) + (fractal.high ? "high" : "low") + "-horizontal-line-" + lastOpositeFractalIndex;
            drawHorizontalLine(index, fractal, name, bars);
        }

        private void drawHorizontalLineForPreviousFractalOfSameSide(Fractal fractal, int middleIndex, Bars bars)
        {
            Fractal previousOfSameSide = fractal.getPreviousOfSameSide();
            if (previousOfSameSide == null)
                return;
            int previousIndex = getPreviousIndex(previousOfSameSide);
            String newLineName = (fractal.high ? "high" : "low") + "-horizontal-line-" + previousIndex;
            drawHorizontalLine(middleIndex, previousOfSameSide, newLineName, bars);
        }

        private void drawHorizontalLine(int index, Fractal fractal, string name, Bars bars)
        {
            Color color = fractal.high ? horizontalTopLineColor.ToCtraderColor() : horizontalBottomLineColor.ToCtraderColor();
            var draw = Chart.DrawTrendLine(name, fractal.dateTime, fractal.value, bars[index].OpenTime, fractal.value, color, 1, LineStyle.Dots);
        }

        private void plotFractalsLink(Fractal fractal1, Fractal fractal2)
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
                plotBadFractalSignal(getPrefix(fractal) + getPreviousIndex(fractal) + "-badSignal-" + i, allWorse[i], bars);
        }

        private void plotBadFractalSignal(String name, Fractal fractal, Bars bars)
        {
            var size = Symbol.PipSize * 4;
            Chart.DrawIcon(name, ChartIconType.Diamond,
               fractal.dateTime, fractal.value + size * (fractal.high ? 1 : -1), Color.Coral);
        }

        private void plotArrow(Fractal fractal, Bars bars)
        {
            String name = getPrefix(fractal) + fractal.index + "-arrow-" + (fractal.high ? "high" : "low");
            Color color = getArrowColor(fractal);
            var size = Symbol.PipSize * 1;
            Chart.DrawIcon(name, fractal.isHigher() ? ChartIconType.UpTriangle : ChartIconType.DownTriangle,
                fractal.dateTime, fractal.value + size * (fractal.high ? 1 : -1), color);
        }

        private static int getPreviousIndex(Fractal fractal)
        {
            Fractal previous = fractal == null ? null : fractal.getPrevious();
            int previousIndex = previous == null ? 0 : previous.index;
            return previousIndex;
        }

        private static Color getArrowColor(Fractal fractal)
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
    }
}