﻿using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class ConfigGuidance : UserControl2
    {
        private readonly FormGPS mf;

        private int lightbarCmPerPixel, lineWidth;
        private double lineLength, lookAheadTime, snapDistance, PanicStopSpeed;

        public ConfigGuidance(Form callingForm)
        {
            mf = callingForm as FormGPS;
            InitializeComponent();
        }

        private void ConfigGuidance_Load(object sender, EventArgs e)
        {
            snapDistance = Properties.Settings.Default.setAS_snapDistance;
            nudSnapDistance.Text = (snapDistance * mf.mToUser).ToString(mf.isMetric ? "0" : "0.0");

            lineLength = Properties.Settings.Default.setAB_lineLength;
            nudABLength.Text = (lineLength * mf.mToUserBig).ToString("0");

            lookAheadTime = Properties.Settings.Default.setAS_guidanceLookAheadTime;
            nudGuidanceLookAhead.Text = lookAheadTime.ToString("0.0");

            lightbarCmPerPixel = Properties.Settings.Default.setDisplay_lightbarCmPerPixel;
            nudLightbarCmPerPixel.Text = lightbarCmPerPixel.ToString("0");

            lineWidth = Properties.Settings.Default.setDisplay_lineWidth;
            nudLineWidth.Text = lineWidth.ToString();

            PanicStopSpeed = Properties.Settings.Default.setVehicle_panicStopSpeed;
            nudPanicStopSpeed.Text = (PanicStopSpeed * mf.KMHToUser).ToString("0.0");

            label20.Text = mf.unitsInCm;
            label79.Text = mf.unitsFtM;
            label102.Text = mf.unitsInCm;
        }

        public override void Close()
        {
            Properties.Settings.Default.setDisplay_lightbarCmPerPixel = mf.lightbarCmPerPixel = lightbarCmPerPixel;
            Properties.Settings.Default.setDisplay_lineWidth = mf.gyd.lineWidth = lineWidth;
            Properties.Settings.Default.setAB_lineLength = mf.gyd.abLength = lineLength;
            Properties.Settings.Default.setAS_snapDistance = snapDistance;
            Properties.Settings.Default.setAS_guidanceLookAheadTime = mf.guidanceLookAheadTime = lookAheadTime;
            Properties.Settings.Default.setVehicle_panicStopSpeed = mf.mc.panicStopSpeed = PanicStopSpeed;

            Properties.Settings.Default.Save();
        }

        private void nudLightbarCmPerPixel_Click(object sender, EventArgs e)
        {
            mf.KeypadToButton(ref nudLightbarCmPerPixel, ref lightbarCmPerPixel, 1, 100);
        }

        private void nudABLength_Click(object sender, EventArgs e)
        {
            mf.KeypadToButton(ref nudABLength, ref lineLength, 200, 5000, 0, mf.mToUserBig, mf.userBigToM);
        }

        private void nudSnapDistance_Click(object sender, EventArgs e)
        {
            mf.KeypadToButton(ref nudSnapDistance, ref snapDistance, 0, 10, mf.isMetric ? 0 : 1, mf.mToUser, mf.userToM);
        }

        private void nudGuidanceLookAhead_Click(object sender, EventArgs e)
        {
            mf.KeypadToButton(ref nudGuidanceLookAhead, ref lookAheadTime, 0, 10, 1);
        }

        private void nudLineWidth_Click(object sender, EventArgs e)
        {
            mf.KeypadToButton(ref nudLineWidth, ref lineWidth, 1, 8);
        }

        private void nudPanicStopSpeed_Click(object sender, EventArgs e)
        {
            mf.KeypadToButton(ref nudPanicStopSpeed, ref PanicStopSpeed, 0.0, 100.0, 1, mf.KMHToUser, mf.userToKMH);
        }

        private void nudLightbarCmPerPixel_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            new FormHelp(gStr.hc_nudLightbarCmPerPixel, gStr.gsHelp).ShowDialog(this);
        }

        private void nudABLength_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            new FormHelp(gStr.hc_nudABLength, gStr.gsHelp).ShowDialog(this);
        }

        private void nudLineWidth_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            new FormHelp(gStr.hc_nudLineWidth, gStr.gsHelp).ShowDialog(this);
        }

        private void nudSnapDistance_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            new FormHelp(gStr.hc_nudSnapDistance, gStr.gsHelp).ShowDialog(this);
        }

        private void nudGuidanceLookAhead_HelpRequested(object sender, HelpEventArgs hlpevent)
        {
            new FormHelp(gStr.hc_nudGuidanceLookAhead, gStr.gsHelp).ShowDialog(this);
        }
    }
}
