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
    public class CameraStateListener : CameraDevice.StateCallback
    {
        private readonly MainActivity owner;

        public CameraStateListener(MainActivity owner)
        {
            if (owner == null)
                throw new System.ArgumentNullException("owner");
            this.owner = owner;
        }

        public override void OnOpened(CameraDevice cameraDevice)
        {
            owner.cameraOpenCloseLock.Release();
            owner.cameraDevice = cameraDevice;
            owner.CreateCameraPreviewSession();
        }

        public override void OnDisconnected(CameraDevice cameraDevice)
        {
            owner.cameraOpenCloseLock.Release();
            cameraDevice.Close();
            owner.cameraDevice = null;
        }

        public override void OnError(CameraDevice cameraDevice, CameraError error)
        {
            owner.cameraOpenCloseLock.Release();
            cameraDevice.Close();
            owner.cameraDevice = null;
            if (owner == null)
                return;
     
        }
    }
}