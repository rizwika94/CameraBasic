using Android.App;
using Android.Content;
using Android.Hardware.Camera2;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CameraBasicDroid.Listener
{
    public class CameraCaptureSessionListener : CameraCaptureSession.StateCallback
    {
        private readonly MainActivity owner;

        public CameraCaptureSessionListener(MainActivity owner)
        {
            if (owner == null)
                throw new System.ArgumentNullException("owner");
            this.owner = owner;
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {

        }

        public override void OnConfigured(CameraCaptureSession session)
        {
            // The camera is already closed
            if (null == owner.cameraDevice)
            {
                return;
            }
            owner.cameraCaptureSession = session;
            try
            {

                owner.captureRequestBuilder.Set(CaptureRequest.ControlAfMode, (int)ControlAFMode.ContinuousPicture);
                // Flash is automatically enabled when necessary.
                owner.SetAutoFlash(owner.captureRequestBuilder);


                owner.previewRequest = owner.captureRequestBuilder.Build();
                owner.cameraCaptureSession.SetRepeatingRequest(owner.previewRequest,
                        owner.captureCallback, owner.backgroundHandler);
            }
            catch (CameraAccessException e)
            {
                e.PrintStackTrace();
            }
        }
    }
}