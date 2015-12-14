using ScpControl.ScpCore;
using ScpControl.Shared.Core;
using ScpControl.Sound;
using ScpControl.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace ScpControl.Usb.Gamepads
{
    public partial class UsbUltraStik360 : UsbDevice
    {
        #region Sounds
        const string switchingXInput = @"Media\SwitchingXInput.flac";
        const string switchingSingle = @"Media\SwitchingSingle.flac";
        const string switchingMerged = @"Media\SwitchingMerged.flac";
        const string switchingArcade = @"Media\SwitchingArcade.flac";
        private static readonly string WorkingDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

        public string SwitchingXInputSoundFile
        {
            get
            {
                return System.IO.Path.IsPathRooted(switchingXInput)
                    ? switchingXInput
                    : System.IO.Path.Combine(WorkingDirectory, switchingXInput);
            }
        }
        public string SwitchingSingleSoundFile
        {
            get
            {
                return System.IO.Path.IsPathRooted(switchingSingle)
                    ? switchingSingle
                    : System.IO.Path.Combine(WorkingDirectory, switchingSingle);
            }
        }
        public string SwitchingMergedSoundFile
        {
            get
            {
                return System.IO.Path.IsPathRooted(switchingMerged)
                    ? switchingMerged
                    : System.IO.Path.Combine(WorkingDirectory, switchingMerged);
            }
        }
        public string SwitchingArcadeSoundFile
        {
            get
            {
                return System.IO.Path.IsPathRooted(switchingArcade)
                    ? switchingArcade
                    : System.IO.Path.Combine(WorkingDirectory, switchingArcade);
            }
        }
        #endregion

        SecretCombo changeToArcade = new SecretCombo(0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80); // 2 - 8 
        SecretCombo changeToXInput = new SecretCombo(0x01, 0x80, 0x40, 0x20, 0x10, 0x08, 0x04, 0x02); // 8 - 2
        SecretCombo mergeOn = new SecretCombo(0x01, 0x10, 0x20, 0x40, 0x80, 0x10, 0x20, 0x40, 0x80); // Bottom row twice
        SecretCombo mergeOff = new SecretCombo(0x01, 0x80, 0x40, 0x20, 0x10, 0x80, 0x40, 0x20, 0x10); // Bottom row twice backwards 

        static Object mergeLock = new Object();
        static byte mergeAxisX = 0x80;
        static byte mergeAxisY = 0x80;
        static byte mergeButtons = 0x00;
        static PhysicalAddress firstJoy = PhysicalAddress.Parse(String.Format("0200{0:X4}{1:X4}", 0xD209, 0x0501));
        static PhysicalAddress secondJoy = PhysicalAddress.Parse(String.Format("0200{0:X4}{1:X4}", 0xD209, 0x0502));

        ButtonMode buttonMode = ButtonMode.Arcade;
        static MergeMode mergeMode = MergeMode.Single;

        private enum ButtonMode
        {
            Arcade,
            XInput
        }

        private enum MergeMode
        {
            Single,
            Merged
        }

        public UsbUltraStik360(int vendorId, int productId) : base(DeviceClassGuid)
        {
            VendorId = vendorId;
            ProductId = productId;

            Model = DsModel.DS4;

            // since these devices have no MAC address, generate one
            DeviceAddress = PhysicalAddress.Parse(String.Format("0200{0:X4}{1:X4}", VendorId, ProductId));
        }

        public UsbUltraStik360(IContainer container) : base(DeviceClassGuid)
        {
            container.Add(this);

            Model = DsModel.DS4;

            // since these devices have no MAC address, generate one
            DeviceAddress = PhysicalAddress.Parse(String.Format("0200{0:X4}{1:X4}", VendorId, ProductId));
        }

        public override bool Open(int instance = 0)
        {
            if(base.Open(instance))
            {
                State = DsState.Reserved;
            }

            return State == DsState.Reserved;
        }

        protected override void ParseHidReport(byte[] report)
        {
            //if (report[0] != 0x01) return;

            PacketCounter++;

            var inputReport = NewHidReport();
            // No support for right axis
            inputReport.Set(Ds4Axis.Rx, 0x80);
            inputReport.Set(Ds4Axis.Ry, 0x80);

            inputReport.PacketCounter = PacketCounter;

            report[0] += 0x80;
            report[1] += 0x80;

            if (mergeMode == MergeMode.Merged && DeviceAddress.Equals(secondJoy))
            {
                lock (mergeLock)
                { 
                    mergeButtons = report[2];
                    mergeAxisX = report[0];
                    mergeAxisY = report[1];
                }
            }
            else
            {
                if (buttonMode == ButtonMode.Arcade)
                {
                    inputReport.Set(Ds4Button.Cross, IsBitSet(report[2], 0));
                    inputReport.Set(Ds4Button.Circle, IsBitSet(report[2], 1));
                    inputReport.Set(Ds4Button.Square, IsBitSet(report[2], 2));
                    inputReport.Set(Ds4Button.Triangle, IsBitSet(report[2], 3));
                    //inputReport.Set(Ds4Axis.R2, IsBitSet(report[2], 3) ? (byte)0xFF : (byte)0x00);
                    inputReport.Set(Ds4Button.L1, IsBitSet(report[2], 4));
                    inputReport.Set(Ds4Button.R1, IsBitSet(report[2], 5));
                    inputReport.Set(Ds4Button.Share, IsBitSet(report[2], 6));
                    inputReport.Set(Ds4Button.Options, IsBitSet(report[2], 7));
                    //inputReport.Set(Ds4Axis.L2, IsBitSet(report[2], 7) ? (byte)0xFF : (byte)0x00);
                    inputReport.Set(Ds4Axis.Lx, report[0]);
                    inputReport.Set(Ds4Axis.Ly, report[1]);

                    if (DeviceAddress.Equals(firstJoy))
                    {
                        if (mergeMode == MergeMode.Merged)
                        {
                            inputReport.Set(Ds4Button.L3, IsBitSet(mergeButtons, 0));
                            inputReport.Set(Ds4Button.R3, IsBitSet(mergeButtons, 1));
                            inputReport.Set(Ds4Axis.R2, IsBitSet(mergeButtons, 2) ? (byte)0xFF : (byte)0x00);
                            inputReport.Set(Ds4Axis.L2, IsBitSet(mergeButtons, 3) ? (byte)0xFF : (byte)0x00);
                        }
                    }
                }
                else if (buttonMode == ButtonMode.XInput)
                {
                    inputReport.Set(Ds4Button.Square, IsBitSet(report[2], 0));
                    inputReport.Set(Ds4Button.Triangle, IsBitSet(report[2], 1));
                    inputReport.Set(Ds4Button.R1, IsBitSet(report[2], 2));
                    inputReport.Set(Ds4Button.L1, IsBitSet(report[2], 3));
                    inputReport.Set(Ds4Button.Cross, IsBitSet(report[2], 4));
                    inputReport.Set(Ds4Button.Circle, IsBitSet(report[2], 5));
                    inputReport.Set(Ds4Axis.R2, IsBitSet(report[2], 6) ? (byte)0xFF : (byte)0x00);
                    inputReport.Set(Ds4Axis.L2, IsBitSet(report[2], 7) ? (byte)0xFF : (byte)0x00);
                    inputReport.Set(Ds4Axis.Lx, report[0]);
                    inputReport.Set(Ds4Axis.Ly, report[1]);

                    if (DeviceAddress.Equals(firstJoy))
                    {
                        if (mergeMode == MergeMode.Merged)
                        {
                            inputReport.Set(Ds4Button.Share, IsBitSet(mergeButtons, 0));
                            inputReport.Set(Ds4Button.Ps, IsBitSet(mergeButtons, 1));
                            inputReport.Set(Ds4Button.Options, IsBitSet(mergeButtons, 2));

                            inputReport.Set(Ds4Axis.Rx, mergeAxisX);
                            inputReport.Set(Ds4Axis.Ry, mergeAxisY);
                        }
                    }
                }
            }

            if (changeToArcade.checkButtons(report[2]))
            {
                AudioPlayer.Instance.PlayCustomFile(SwitchingArcadeSoundFile);
                buttonMode = ButtonMode.Arcade;
            }
            if (changeToXInput.checkButtons(report[2]))
            {
                AudioPlayer.Instance.PlayCustomFile(SwitchingXInputSoundFile);
                buttonMode = ButtonMode.XInput;
            }
            if (DeviceAddress.Equals(firstJoy))
            {
                if (mergeOn.checkButtons(report[2]))
                {
                    AudioPlayer.Instance.PlayCustomFile(SwitchingMergedSoundFile);
                    mergeMode = MergeMode.Merged;
                }
                if (mergeOff.checkButtons(report[2]))
                {
                    AudioPlayer.Instance.PlayCustomFile(SwitchingSingleSoundFile);
                    mergeMode = MergeMode.Single;
                }
            }

            OnHidReportReceived(inputReport);
        }

        public static Guid DeviceClassGuid
        {
            get { return Guid.Parse("{2ED90CE1-376F-4982-8F7F-E056CBC3CA71}"); }
        }

        public override DsPadId PadId { get; set; }
    }
}
