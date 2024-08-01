using System.Collections.Generic;

namespace VideoAudioMediaPlayer
{
    public static class StringArrayExtensions
    {
        public static double[] Compress(this double[] source, int distance)
        {
            if (source == null || source.Length == 0)
                return new double[0];

            List<double> result = new List<double>();
            double? lastAdded = null;

            foreach (var number in source)
            {
                if (lastAdded == null || (number - lastAdded.Value >= distance))
                {
                    result.Add(number);
                    lastAdded = number;
                }
            }

            return result.ToArray();
        }
    }
}
