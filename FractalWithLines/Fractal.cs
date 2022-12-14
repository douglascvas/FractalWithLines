﻿using System;
using System.Collections.Generic;

namespace cAlgo
{
    public class Fractal
    {
        public String prefix { get; set; }
        public int index { get; set; }
        public double value { get; set; }
        public bool high { get; set; }
        public bool low
        {
            get { return !high; }
        }

        public DateTime dateTime { get; set; }

        private Fractal previousFractal { get; set; }
        private Fractal nextFractal { get; set; }

        public Fractal(int index, DateTime dateTime, double value, bool high, String prefix)
        {
            this.index = index;
            this.value = value;
            this.high = high;
            this.dateTime = dateTime;
            this.prefix = prefix;
        }

        public bool isHigher()
        {
            Fractal previousSameSide = getPreviousOfSameSide();
            if (previousSameSide == null)
                return true;
            return previousSameSide.value < value;
        }

        public Fractal getPrevious(bool filterBest = true)
        {
            if (!filterBest)
            {
                return previousFractal;
            }
            Fractal previous = getFirstOfBlock().previousFractal;
            if (previous == null)
                return null;
            return filterBest ? previous.getBest() : previous;
        }

        public Fractal getPreviousOfSameSide(bool filterBest = true)
        {
            Fractal previous = getPrevious();
            if (previous == null)
                return null;
            return previous.getPrevious(filterBest);
        }

        public Fractal getNext(bool filterBest = true)
        {
            if (!filterBest)
            {
                return nextFractal;
            }
            Fractal next = getBest().nextFractal;
            if (next == null)
                return null;
            return next.getBest();
        }

        public void addFractal(Fractal fractal)
        {
            nextFractal = fractal;
            fractal.previousFractal = this;
        }

        public Fractal getFirstOfBlock()
        {
            Fractal first = this;
            while (first.previousFractal != null && first.previousFractal.high == high)
                first = first.previousFractal;
            return first;
        }

        public Fractal getBest()
        {
            Fractal correctFractal = this;
            // search previous
            Fractal fractal = previousFractal;
            while (fractal != null && fractal.high == high)
            {
                if (fractal.high && fractal.value > correctFractal.value ||
                    !fractal.high && fractal.value < correctFractal.value)
                    correctFractal = fractal;
                fractal = fractal.previousFractal;
            }
            // search next
            fractal = nextFractal;
            while (fractal != null && fractal.high == high)
            {
                if (fractal.high && fractal.value > correctFractal.value ||
                    !fractal.high && fractal.value < correctFractal.value)
                    correctFractal = fractal;
                fractal = fractal.nextFractal;
            }
            return correctFractal;
        }

        public List<Fractal> getBadFractals()
        {
            Fractal best = getBest();
            List<Fractal> result = new List<Fractal>();
            Fractal fractal = best.previousFractal;
            // search previous
            while (fractal != null && fractal.high == high)
            {
                result.Add(fractal);
                fractal = fractal.previousFractal;
            }
            // search next
            fractal = best.nextFractal;
            while (fractal != null && fractal.high == high)
            {
                result.Add(fractal);
                fractal = fractal.nextFractal;
            }
            return result;
        }

        public FractalType getFractalType()
        {
            if (high && isHigher())
                return FractalType.HigherHigh;
            if (!high && isHigher())
                return FractalType.HigherLow;
            if (high && !isHigher())
                return FractalType.LowerHigh;
            return FractalType.LowerLow;
        }

        public void remove()
        {
            removeFractal(this);
        }

        // Remove all of same side
        public void removeBlock()
        {
            // Remove previous
            Fractal fractal = this;
            while (fractal != null && fractal.high == high)
            {
                removeFractal(fractal);
                fractal = fractal.previousFractal;
            }

            // Remove next
            fractal = nextFractal;
            while (fractal != null && fractal.high == high)
            {
                removeFractal(fractal);
                fractal = fractal.nextFractal;
            }
        }

        private void removeFractal(Fractal fractal)
        {
            Fractal previous = fractal.previousFractal;
            Fractal next = fractal.nextFractal;
            if (previous != null)
                previous.nextFractal = next;
            if (next != null)
                next.previousFractal = previous;
        }
    }
}