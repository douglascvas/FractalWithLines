using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo
{
    public class NotificationManager
    {
        private readonly Algo algo;
        private readonly string email;
        private readonly string highSound;
        private readonly string lowSound;

        public NotificationManager(Algo algo, string email, string highSound, string lowSound)
        {
            this.algo = algo;
            this.email = email;
            this.highSound = highSound;
            this.lowSound = lowSound;
        }

        public void sendEmailNotification(Fractal fractal)
        {
            string fractalType;
            if (fractal.getFractalType() == FractalType.HigherHigh)
                fractalType = "higher high";
            else if (fractal.getFractalType() == FractalType.HigherLow)
                fractalType = "higher low";
            else if (fractal.getFractalType() == FractalType.LowerHigh)
                fractalType = "lower high";
            else
                fractalType = "lower low";

            string notificationEmailBody = "You have a new " + fractalType + " fractal on symbol " + algo.Symbol.Code + ".";
            if (email.Length > 0)
            {
                algo.Print("Sending email to {0}", email);
                algo.Notifications.SendEmail("lizalves.alves@gmail.com", email, "New " + fractalType + " fractal", notificationEmailBody);
            }
        }

        public void playSoundNotification(Fractal fractal)
        {
            if (fractal.high && highSound.Length > 0)
               algo.Notifications.PlaySound(highSound);
            else if (fractal.low && lowSound.Length > 0)
                algo.Notifications.PlaySound(lowSound);
        }
    }
}
