using Android.Util;
using Java.Lang;
using Java.Util;
using System;

namespace CameraBasicDroid
{
    public class CompareSizesByArea : Java.Lang.Object, IComparator
    {
        public int Compare(Java.Lang.Object o1, Java.Lang.Object o2)
        {
            var lhsSize = (Size)o1;
            var rhsSize = (Size)o2;
            return Long.Signum((long)lhsSize.Width * lhsSize.Height - (long)rhsSize.Width * rhsSize.Height);
        }
    }
}