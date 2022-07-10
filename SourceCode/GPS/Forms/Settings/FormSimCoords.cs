﻿using System;
using System.Windows.Forms;

namespace AgOpenGPS
{
    public partial class FormSimCoords : Form
    {
        //class variables
        private readonly FormGPS mf = null;

        public FormSimCoords(Form callingForm)
        {
            //get copy of the calling main form
            mf = callingForm as FormGPS;
            InitializeComponent();

            this.Text = gStr.gsEnterCoordinatesForSimulator;

            nudLatitude.Controls[0].Enabled = false;
            nudLongitude.Controls[0].Enabled = false;
        }

        private void FormSimCoords_Load(object sender, EventArgs e)
        {
            nudLatitude.Value = (decimal)Properties.Settings.Default.setGPS_SimLatitude;
            nudLongitude.Value = (decimal)Properties.Settings.Default.setGPS_SimLongitude;
        }

        private void bntOK_Click(object sender, EventArgs e)
        {
            if (mf.isJobStarted)
            {
                mf.TimedMessageBox(2000, gStr.gsFieldIsOpen, gStr.gsCloseFieldFirst);
                Close();
            }

            if (!glm.isSimEnabled)
            {
                mf.TimedMessageBox(2000, "Simulator is off", "Go Back To Work, No Time For Games");
                Close();
            }

            mf.worldManager.latStart = (double)nudLatitude.Value;
            mf.worldManager.lonStart = (double)nudLongitude.Value;

            mf.worldManager.SetLocalMetersPerDegree();

            mf.sim.resetSim();

            Properties.Settings.Default.setGPS_SimLatitude = mf.worldManager.latStart;
            Properties.Settings.Default.setGPS_SimLongitude = mf.worldManager.lonStart;
            Properties.Settings.Default.Save();
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void nudLongitude_Click(object sender, EventArgs e)
        {
            nudLongitude.KeypadToNUD();
        }
        private void nudLatitude_Click(object sender, EventArgs e)
        {
            nudLatitude.KeypadToNUD();
        }
    }
}