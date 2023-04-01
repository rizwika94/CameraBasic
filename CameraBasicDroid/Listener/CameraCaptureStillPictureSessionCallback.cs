using Android.Hardware.Camera2;
using Android.Util;

namespace CameraBasicDroid.Listener
{
    public class CameraCaptureStillPictureSessionCallback : CameraCaptureSession.CaptureCallback
    {

        private readonly MainActivity owner;

        public CameraCaptureStillPictureSessionCallback(MainActivity owner)
        {
            if (owner == null)
                throw new System.ArgumentNullException("owner");
            this.owner = owner;
        }

        public override void OnCaptureCompleted(CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
        {
            owner.UnlockFocus();
        }
    }
}