﻿namespace AgOpenGPS
{
    public class CModuleComm
    {
        //copy of the mainform address
        private readonly FormGPS mf;

        //Critical Safety Properties
        public bool isOutOfBounds = false;

        // ---- Section control switches to AOG  ---------------------------------------------------------
        //PGN - 32736 - 127.249 0x7FF9
        public byte[] ssP = new byte[3];

        public int
            swHeader = 0,
            swMain = 5,
            swReserve = 6,
            swReserve2 = 7,
            swNumSections = 8,
            swOnGr0 = 9,
            swOffGr0 = 10,
            swOnGr1 = 11,
            swOffGr1 = 12;

        public int pwmDisplay = 0;
        public double actualSteerAngleDegrees = 0;
        public int actualSteerAngleChart = 0, sensorData = -1;

        public int toolPWM = 0, toolStatus = 0;
        public double toolActualDistance = 0, toolError = 0;

        //for the workswitch
        public bool isWorkSwitchActiveLow, isWorkSwitchEnabled, isWorkSwitchManual, isSteerControlsManual;

        public bool workSwitchHigh, oldWorkSwitchHigh, steerSwitchHigh, oldsteerSwitchHigh;

        //constructor
        public CModuleComm(FormGPS _f)
        {
            mf = _f;
            //WorkSwitch logic
            isWorkSwitchEnabled = false;

            //does a low, grounded out, mean on
            isWorkSwitchActiveLow = true;
        }

        //Called from "OpenGL.Designer.cs" when requied
        public void CheckWorkAndSteerSwitch()
        {
            //AutoSteerAuto button enable - Ray Bear inspired code - Thx Ray!
            if (mf.ahrs.isAutoSteerAuto && steerSwitchHigh != oldsteerSwitchHigh)
            {
                oldsteerSwitchHigh = steerSwitchHigh;
                //steerSwith is active low
                mf.setBtnAutoSteer(!steerSwitchHigh);
            }

            if (isSteerControlsManual) workSwitchHigh = steerSwitchHigh;

            if ((isWorkSwitchEnabled || isSteerControlsManual) && workSwitchHigh != oldWorkSwitchHigh)
            {
                oldWorkSwitchHigh = workSwitchHigh;

                if (workSwitchHigh != isWorkSwitchActiveLow)
                    mf.setSectionBtnState(isWorkSwitchManual ? btnStates.On : btnStates.Auto);
                else//Checks both on-screen buttons, performs click if button is not off
                    mf.setSectionBtnState(btnStates.Off);
            }
        }
    }
}