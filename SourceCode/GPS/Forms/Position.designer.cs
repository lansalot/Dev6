﻿//Please, if you use this, share the improvements

using System;
using System.Collections.Generic;
using System.Text;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        //very first fix to setup grid etc
        public bool isFirstFixPositionSet = false, isGPSPositionInitialized = false, isFirstHeadingSet = false,
            isReverse = false, isSuperSlow = false;

        //string to record fixes for elevation maps
        public StringBuilder sbFix = new StringBuilder();

        // autosteer variables for sending serial
        public short guidanceLineDistanceOff = 32000, guidanceLineSteerAngle;

        public short errorAngVel;
        public double setAngVel;
        public bool isAngVelGuidance;

        //guidance line look ahead
        public double guidanceLookAheadTime = 2;

        //how many fix updates per sec
        public int fixUpdateHz = 10;
        public double fixUpdateTime = 0.1;

        public vec2 pivotAxlePos = new vec2(0, 0);
        public vec2 steerAxlePos = new vec2(0, 0);
        public vec3 toolPos = new vec3(0, 0, 0);
        public vec3 tankPos = new vec3(0, 0, 0);
        public vec2 hitchPos = new vec2(0, 0);

        //history
        public vec2 prevFix = new vec2(0, 0);
        public vec2 lastGPS = new vec2(0, 0);
        public vec2 lastReverseFix = new vec2(0, 0);
        public vec2 prevSectionPos = new vec2(0, 0);
        public vec2 prevBoundaryPos = new vec2(0, 0);

        //headings
        public double fixHeading = 0.0, camHeading = 0.0, smoothCamHeading = 0;

        //storage for the cos and sin of heading
        public double cosSectionHeading = 1.0, sinSectionHeading = 0.0;

        //how far travelled since last section was added, section points
        double sectionTriggerDistance = 0, sectionTriggerStepDistance = 0;

        //Everything is so wonky at the start
        int startCounter = 0;

        //individual points for the flags in a list
        public List<CFlag> flagPts = new List<CFlag>();

        public double avgSpeed;//for average speed
        public int crossTrackError;

        //youturn
        public double distancePivotToTurnLine = -4444;

        //IMU 
        public double rollCorrectionDistance = 0;
        public double imuGPS_Offset, imuCorrected;

        //step position - slow speed spinner killer
        private int currentStepFix = 0;
        private const int totalFixSteps = 20;
        public vecFix2Fix[] stepFixPts = new vecFix2Fix[totalFixSteps];
        public double distanceCurrentStepFix = 0, minFixStepDist = 1, startSpeed = 0.5;

        private double nowHz = 0;

        public bool isRTK, isRTK_KillAutosteer;

        public double uncorrectedEastingGraph = 0;
        public double correctionDistanceGraph = 0;

        public void UpdateFixPosition()
        {
            //Measure the frequency of the GPS updates
            swHz.Stop();
            nowHz = ((double)System.Diagnostics.Stopwatch.Frequency) / (double)swHz.ElapsedTicks;

            //simple comp filter
            if (nowHz < 20) HzTime = 0.97 * HzTime + 0.03 * nowHz;

            //auto set gps freq
            fixUpdateHz = (int)(HzTime + 0.5);
            fixUpdateTime = 1 / HzTime;

            swHz.Reset();
            swHz.Start();

            //start the watch and time till it finishes
            swFrame.Reset();
            swFrame.Start();

            startCounter++;

            if (!isGPSPositionInitialized)
            {
                InitializeFirstFewGPSPositions();
                return;
            }

            if (pn.headingTrueDual == double.MaxValue)
            {
                //calculate current heading only when moving, otherwise use last
                if (!isFirstHeadingSet) //set in steer settings, Stanley
                {
                    if (Math.Abs(avgSpeed) >= 1.5)
                    {
                        prevFix.easting = stepFixPts[0].easting; prevFix.northing = stepFixPts[0].northing;

                        if (stepFixPts[2].isSet == 0)
                        {
                            //this is the first position no roll or offset correction
                            if (stepFixPts[0].isSet == 0)
                            {
                                stepFixPts[0].easting = pn.fix.easting;
                                stepFixPts[0].northing = pn.fix.northing;
                                stepFixPts[0].isSet = 1;
                                return;
                            }

                            //and the second
                            if (stepFixPts[1].isSet == 0)
                            {
                                for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                                stepFixPts[0].easting = pn.fix.easting;
                                stepFixPts[0].northing = pn.fix.northing;
                                stepFixPts[0].isSet = 1;
                                return;
                            }

                            //the critcal moment for checking initial direction/heading.
                            for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                            stepFixPts[0].easting = pn.fix.easting;
                            stepFixPts[0].northing = pn.fix.northing;
                            stepFixPts[0].isSet = 1;

                            fixHeading = Math.Atan2(pn.fix.easting - stepFixPts[2].easting,
                                pn.fix.northing - stepFixPts[2].northing);

                            if (fixHeading < 0) fixHeading += glm.twoPI;
                            else if (fixHeading > glm.twoPI) fixHeading -= glm.twoPI;

                            //now we have a heading, fix the first 3
                            if (vehicle.antennaOffset != 0)
                            {
                                for (int i = 0; i < 3; i++)
                                {
                                    stepFixPts[i].easting = (Math.Cos(-fixHeading) * vehicle.antennaOffset) + stepFixPts[i].easting;
                                    stepFixPts[i].northing = (Math.Sin(-fixHeading) * vehicle.antennaOffset) + stepFixPts[i].northing;
                                }
                            }

                            if (ahrs.imuRoll != 88888)
                            {

                                //change for roll to the right is positive times -1
                                rollCorrectionDistance = Math.Tan(glm.toRadians((ahrs.imuRoll))) * -vehicle.antennaHeight;

                                // roll to left is positive  **** important!!
                                // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                                for (int i = 0; i < 3; i++)
                                {
                                    stepFixPts[i].easting = (Math.Cos(-fixHeading) * rollCorrectionDistance) + stepFixPts[i].easting;
                                    stepFixPts[i].northing = (Math.Sin(-fixHeading) * rollCorrectionDistance) + stepFixPts[i].northing;
                                }
                            }

                            //get the distance from first to 2nd point, update fix with new offset/roll
                            stepFixPts[0].distance = glm.Distance(stepFixPts[1], stepFixPts[0]);
                            pn.fix.easting = stepFixPts[0].easting;
                            pn.fix.northing = stepFixPts[0].northing;

                            isFirstHeadingSet = true;
                            TimedMessageBox(2000, "Direction Reset", "Forward is Set");
                        }
                    }
                    else return;
                }
                else
                {
                    if (vehicle.antennaOffset != 0)
                    {
                        pn.fix.easting = (Math.Cos(-fixHeading) * vehicle.antennaOffset) + pn.fix.easting;
                        pn.fix.northing = (Math.Sin(-fixHeading) * vehicle.antennaOffset) + pn.fix.northing;
                    }

                    uncorrectedEastingGraph = pn.fix.easting;

                    //originalEasting = pn.fix.easting;
                    if (ahrs.imuRoll != 88888 && vehicle.antennaHeight > 0)
                    {
                        //change for roll to the right is positive times -1
                        rollCorrectionDistance = Math.Sin(glm.toRadians((ahrs.imuRoll))) * -vehicle.antennaHeight;
                        correctionDistanceGraph = rollCorrectionDistance;

                        // roll to left is positive  **** important!!
                        // not any more - April 30, 2019 - roll to right is positive Now! Still Important
                        pn.fix.easting = (Math.Cos(-fixHeading) * rollCorrectionDistance) + pn.fix.easting;
                        pn.fix.northing = (Math.Sin(-fixHeading) * rollCorrectionDistance) + pn.fix.northing;
                    }

                    //initializing all done
                    if (Math.Abs(avgSpeed) > startSpeed)
                    {
                        isSuperSlow = false;

                        //how far since last fix
                        distanceCurrentStepFix = glm.Distance(stepFixPts[0], pn.fix);

                        if (stepFixPts[0].isSet == 0)
                            distanceCurrentStepFix = 0;

                        //save current fix and distance and set as valid
                        for (int i = totalFixSteps - 1; i > 0; i--) stepFixPts[i] = stepFixPts[i - 1];
                        stepFixPts[0].easting = pn.fix.easting;
                        stepFixPts[0].northing = pn.fix.northing;
                        stepFixPts[0].isSet = 1;
                        stepFixPts[0].distance = distanceCurrentStepFix;

                        if (stepFixPts[3].isSet == 1)
                        {
                            //find back the fix to fix distance, then heading
                            double dist = 0;
                            for (int i = 1; i < totalFixSteps; i++)
                            {
                                if (stepFixPts[i].isSet == 0)
                                {
                                    currentStepFix = i - 1;
                                    break;
                                }
                                dist += stepFixPts[i - 1].distance;
                                currentStepFix = i;
                                if (dist > minFixStepDist)
                                    break;
                            }

                            //most recent heading
                            double newHeading = Math.Atan2(pn.fix.easting - stepFixPts[currentStepFix].easting,
                                                        pn.fix.northing - stepFixPts[currentStepFix].northing);
                            if (newHeading < 0) newHeading += glm.twoPI;

                            //update the last gps for slow speed.
                            lastGPS = pn.fix;

                            if (ahrs.isReverseOn)
                            {
                                //what is angle between the last valid heading before stopping and one just now
                                double delta = Math.Abs(Math.PI - Math.Abs(Math.Abs(newHeading - fixHeading) - Math.PI));

                                //ie change in direction
                                if (delta > 1.57) //
                                {
                                    isReverse = true;
                                    newHeading += Math.PI;
                                    if (newHeading < 0) newHeading += glm.twoPI;
                                    else if (newHeading >= glm.twoPI) newHeading -= glm.twoPI;
                                }
                                else
                                    isReverse = false;
                            }

                            fixHeading = newHeading - glm.toRadians(vehicle.antennaPivot / 1
                                    * mc.actualSteerAngleDegrees * (isReverse ? ahrs.reverseComp : ahrs.forwardComp));
                            if (fixHeading < 0) fixHeading += glm.twoPI;
                            else if (fixHeading >= glm.twoPI) fixHeading -= glm.twoPI;
                        }
                    }
                    //slow speed and reverse
                    else
                    {
                        isSuperSlow = true;

                        if (stepFixPts[0].isSet == 1)
                        {
                            //going back and forth clear out the array so only 1 value
                            for (int i = 0; i < stepFixPts.Length; i++)
                            {
                                stepFixPts[i].easting = 0;
                                stepFixPts[i].northing = 0;
                                stepFixPts[i].isSet = 0;
                                stepFixPts[i].distance = 0;
                            }
                        }

                        //if (!ahrs.isReverseOn) goto byPass;

                        //how far since last fix
                        distanceCurrentStepFix = glm.Distance(lastGPS, pn.fix);

                        //new heading if exceeded fix heading step distance
                        if (distanceCurrentStepFix > minFixStepDist)
                        {
                            //most recent heading
                            double newHeading = Math.Atan2(pn.fix.easting - lastGPS.easting,
                                                        pn.fix.northing - lastGPS.northing);

                            //Pointing the opposite way the fixes are moving
                            //if (vehicle.isReverse) gpsHeading += Math.PI;
                            if (newHeading < 0) newHeading += glm.twoPI;

                            if (ahrs.isReverseOn)
                            {
                                //what is angle between the last valid heading before stopping and one just now
                                double delta = Math.Abs(Math.PI - Math.Abs(Math.Abs(newHeading - fixHeading) - Math.PI));

                                //ie change in direction
                                {
                                    if (delta > 1.57) //
                                    {
                                        isReverse = true;
                                        newHeading += Math.PI;
                                        if (newHeading < 0) newHeading += glm.twoPI;
                                        if (newHeading >= glm.twoPI) newHeading -= glm.twoPI;
                                    }
                                    else
                                        isReverse = false;
                                }
                            }

                            //set the headings
                            fixHeading = newHeading;

                            lastGPS.easting = pn.fix.easting;
                            lastGPS.northing = pn.fix.northing;
                        }
                        else
                            return;
                    }

                    // IMU Fusion with heading correction, add the correction
                    if (ahrs.imuHeading != 99999)
                    {
                        //current gyro angle in radians
                        double imuHeading = (glm.toRadians(ahrs.imuHeading));

                        //Difference between the IMU heading and the GPS heading
                        double gyroDelta = (imuHeading + imuGPS_Offset) - fixHeading;
                        //double gyroDelta = Math.Abs(Math.PI - Math.Abs(Math.Abs(imuHeading + gyroCorrection) - gpsHeading) - Math.PI);

                        if (gyroDelta < 0) gyroDelta += glm.twoPI;
                        else if (gyroDelta > glm.twoPI) gyroDelta -= glm.twoPI;

                        //calculate delta based on circular data problem 0 to 360 to 0, clamp to +- 2 Pi
                        if (gyroDelta >= -glm.PIBy2 && gyroDelta <= glm.PIBy2) gyroDelta *= -1.0;
                        else
                        {
                            if (gyroDelta > glm.PIBy2) { gyroDelta = glm.twoPI - gyroDelta; }
                            else { gyroDelta = (glm.twoPI + gyroDelta) * -1.0; }
                        }
                        if (gyroDelta > glm.twoPI) gyroDelta -= glm.twoPI;
                        else if (gyroDelta < -glm.twoPI) gyroDelta += glm.twoPI;

                        if (Math.Abs(avgSpeed) > startSpeed)
                        {
                            if (isReverse)
                                imuGPS_Offset += (gyroDelta * (0.01));
                            else
                                imuGPS_Offset += (gyroDelta * (ahrs.fusionWeight));
                        }
                        else
                        {
                            imuGPS_Offset += (gyroDelta * (0.2));
                        }

                        if (imuGPS_Offset > glm.twoPI) imuGPS_Offset -= glm.twoPI;
                        else if (imuGPS_Offset < 0) imuGPS_Offset += glm.twoPI;

                        //determine the Corrected heading based on gyro and GPS
                        imuCorrected = imuHeading + imuGPS_Offset;
                        if (imuCorrected > glm.twoPI) imuCorrected -= glm.twoPI;
                        else if (imuCorrected < 0) imuCorrected += glm.twoPI;

                        //use imu as heading when going slow
                        fixHeading = imuCorrected;
                    }
                }
            }
            else
            {
                isFirstHeadingSet = true;
                //use Dual Antenna heading for camera and tractor graphic
                fixHeading = glm.toRadians(pn.headingTrueDual);

                uncorrectedEastingGraph = pn.fix.easting;

                if (glm.DistanceSquared(lastReverseFix, pn.fix) > 0.6)
                {
                    //most recent heading
                    double newHeading = Math.Atan2(pn.fix.easting - lastReverseFix.easting,
                                                pn.fix.northing - lastReverseFix.northing);

                    //what is angle between the last reverse heading and current dual heading
                    double delta = Math.Abs(Math.PI - Math.Abs(Math.Abs(newHeading - fixHeading) - Math.PI));

                    //are we going backwards
                    isReverse = delta > 2 ? true : false;

                    //save for next meter check
                    lastReverseFix = pn.fix;
                }

                if (vehicle.antennaOffset != 0)
                {
                    pn.fix.easting = (Math.Cos(-fixHeading) * vehicle.antennaOffset) + pn.fix.easting;
                    pn.fix.northing = (Math.Sin(-fixHeading) * vehicle.antennaOffset) + pn.fix.northing;
                }

                if (ahrs.imuRoll != 88888 && vehicle.antennaHeight != 0)
                {

                    //change for roll to the right is positive times -1
                    rollCorrectionDistance = Math.Sin(glm.toRadians((ahrs.imuRoll))) * -vehicle.antennaHeight;
                    correctionDistanceGraph = rollCorrectionDistance;

                    pn.fix.easting = (Math.Cos(-fixHeading) * rollCorrectionDistance) + pn.fix.easting;
                    pn.fix.northing = (Math.Sin(-fixHeading) * rollCorrectionDistance) + pn.fix.northing;
                }

                //grab the most current fix and save the distance from the last fix
                distanceCurrentStepFix = glm.Distance(pn.fix, prevFix);

                //most recent fixes are now the prev ones
                prevFix.easting = pn.fix.easting; prevFix.northing = pn.fix.northing;
            }

            double distance = (fixHeading - smoothCamHeading) % glm.twoPI;
            if (distance < -glm.PIBy2) distance += glm.twoPI;
            if (distance > glm.PIBy2) distance -= glm.twoPI;

            smoothCamHeading += distance * worldManager.camSmoothFactor;

            if (smoothCamHeading > glm.twoPI) smoothCamHeading -= glm.twoPI;
            else if (smoothCamHeading < 0) smoothCamHeading += glm.twoPI;

            camHeading = glm.toDegrees(smoothCamHeading);

            TheRest();

            #region AutoSteer

            //preset the values
            guidanceLineDistanceOff = 32000;

            gyd.GetCurrentGuidanceLine(pivotAxlePos, steerAxlePos, fixHeading);

            // If Drive button off - normal autosteer 
            if (!vehicle.isInFreeDriveMode)
            {
                //fill up0 the appropriate arrays with new values
                p_254.pgn[p_254.speedHi] = unchecked((byte)((int)(Math.Abs(pn.speed) * 10.0) >> 8));
                p_254.pgn[p_254.speedLo] = unchecked((byte)((int)(Math.Abs(pn.speed) * 10.0)));

                //save distance for display in millimeters
                avgPivDistance = avgPivDistance * 0.5 + guidanceLineDistanceOff * 0.5;

                if ((isAutoSteerBtnOn || gyd.isDrivingRecordedPath) && guidanceLineDistanceOff != 32000) //32000 means auto steer is off
                {
                    p_254.pgn[p_254.status] = 1;
                }
                else p_254.pgn[p_254.status] = 0;

                //convert to cm from mm and divide by 2 - lightbar
                int distanceX2;
                if (p_254.pgn[p_254.status] == 0)
                    distanceX2 = 255;
                else
                {
                    distanceX2 = (int)(guidanceLineDistanceOff * 0.05);

                    if (distanceX2 < -127) distanceX2 = -127;
                    else if (distanceX2 > 127) distanceX2 = 127;
                    distanceX2 += 127;
                }

                p_254.pgn[p_254.lineDistance] = unchecked((byte)distanceX2);

                if (isAngVelGuidance)
                {
                    errorAngVel = (short)(((int)(setAngVel) - ahrs.angVel));

                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(errorAngVel >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(errorAngVel));
                }
                else
                {
                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(guidanceLineSteerAngle >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(guidanceLineSteerAngle));
                }
            }
            else //Drive button is on
            {
                //fill up the auto steer array with free drive values
                p_254.pgn[p_254.speedHi] = unchecked((byte)((int)(80) >> 8));
                p_254.pgn[p_254.speedLo] = unchecked((byte)((int)(80)));

                //turn on status to operate
                p_254.pgn[p_254.status] = 1;

                //send the steer angle
                guidanceLineSteerAngle = (Int16)(vehicle.driveFreeSteerAngle * 100);

                if (isAngVelGuidance)
                {
                    setAngVel = 0.277777 * avgSpeed * (Math.Tan(glm.toRadians(vehicle.driveFreeSteerAngle))) / vehicle.wheelbase;
                    setAngVel = glm.toDegrees(setAngVel) * 100;

                    errorAngVel = (short)(((int)(setAngVel) - ahrs.angVel));

                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(errorAngVel >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(errorAngVel));
                }
                else
                {
                    p_254.pgn[p_254.steerAngleHi] = unchecked((byte)(guidanceLineSteerAngle >> 8));
                    p_254.pgn[p_254.steerAngleLo] = unchecked((byte)(guidanceLineSteerAngle));

                }
            }

            //out serial to autosteer module  //indivdual classes load the distance and heading deltas 
            SendPgnToLoop(p_254.pgn);

            //for average cross track error
            if (guidanceLineDistanceOff != 32000)
            {
                crossTrackError = (int)((double)crossTrackError * 0.90 + Math.Abs((double)guidanceLineDistanceOff) * 0.1);
            }

            #endregion

            #region Youturn

            //reset the fault distance to an appropriate weird number
            // -4444 means cross trac error too high
            distancePivotToTurnLine = -4444;

            //always force out of bounds and change only if in bounds after proven so
            mc.isOutOfBounds = true;

            //if an outer boundary is set, then apply critical stop logic
            if (bnd.bndList.Count > 0)
            {
                //Are we inside outer and outside inner all turn boundaries, no turn creation problems
                if (bnd.IsPointInsideFenceArea(pivotAxlePos) && !gyd.isTurnCreationTooClose && !gyd.isTurnCreationNotCrossingError)
                {
                    //reset critical stop for bounds violation
                    mc.isOutOfBounds = false;

                    //do the auto youturn logic if everything is on.
                    if (gyd.isYouTurnBtnOn && isAutoSteerBtnOn && !gyd.isYouTurnTriggered)
                    {
                        //if we are too much off track > 1.3m, kill the diagnostic creation, start again
                        if (crossTrackError > 1300)
                            gyd.ResetCreatedYouTurn();
                        else
                        {
                            //now check to make sure we are not in an inner turn boundary - drive thru is ok
                            if (gyd.youTurnPhase < 0)
                                gyd.youTurnPhase++;
                            else if (gyd.youTurnPhase < 255)
                            {
                                gyd.FindTurnPoints(pivotAxlePos);

                                if (gyd.youTurnPhase == 254) gyd.SmoothYouTurn(gyd.uTurnSmoothing);
                            }
                            else if (gyd.ytList.Count > 1) //wait to trigger the actual turn since its made and waiting
                            {
                                //distance from current pivot to first point of youturn pattern
                                distancePivotToTurnLine = glm.Distance(gyd.ytList[0], pivotAxlePos);

                                if (distancePivotToTurnLine < 20.0 && distancePivotToTurnLine >= 18.0 && !sounds.isBoundAlarming)
                                {
                                    if (sounds.isTurnSoundOn) sounds.sndBoundaryAlarm.Play();
                                    sounds.isBoundAlarming = true;
                                }

                                //if we are close enough to pattern, trigger.
                                if (distancePivotToTurnLine <= 10.0 && pivotAxlePos.isInFront(gyd.ytList[0] - gyd.ytList[1], gyd.ytList[0], gyd.ytList[1]))
                                {
                                    gyd.YouTurnTrigger();
                                    sounds.isBoundAlarming = false;
                                }
                            }
                            else gyd.TurnCreationWidthError = true;
                        }
                    } // end of isInWorkingArea
                }
                // here is stop logic for out of bounds - in an inner or out the outer turn border.
                else
                {
                    mc.isOutOfBounds = true;
                    if (gyd.isYouTurnBtnOn)
                    {
                        gyd.ResetCreatedYouTurn();
                        sim.stepDistance = 0;
                    }
                }
            }
            else
            {
                mc.isOutOfBounds = false;
            }

            #endregion

            //draw the section control window off screen buffer
            if (isJobStarted && (isFastSections || bbCounter++ > 0))
            {
                bbCounter = 0;
                oglBack.Refresh();
                SendPgnToLoop(p_239.pgn);
            }

            //update main window
            oglMain.MakeCurrent();
            oglMain.Refresh();

            if (isAutoDayNight && dayNightCounter > 600)//10 minutes
            {
                dayNightCounter = 1;

                if (sunrise.Date != DateTime.Today)
                {
                    IsBetweenSunriseSunset(pn.latitude, pn.longitude);
                }

                isDayTime = (DateTime.Now.Ticks < sunset.Ticks && DateTime.Now.Ticks > sunrise.Ticks);

                if (isDayTime != isDay)
                {
                    isDay = !isDayTime;
                    SwapDayNightMode();
                }
            }

            //if a minute has elapsed save the field in case of crash and to be able to resume            
            if (isJobStarted && secondsCounter > 30 && sentenceCounter < 20)
            {
                secondsCounter = 1;
                tmrWatchdog.Enabled = false;

                //don't save if no gps
                if (isJobStarted)
                {
                    //auto save the field patches, contours accumulated so far
                    FileSaveSections();
                    FileSaveContour();

                    //NMEA log file
                    if (isLogElevation) FileSaveElevation();
                    //FileSaveFieldKML();
                }
                //go see if data ready for draw and position updates
                tmrWatchdog.Enabled = true;
            }

            //end of UppdateFixPosition
            swFrame.Stop();

            //stop the timer and calc how long it took to do calcs and draw
            double frameTimeRough = (double)swFrame.ElapsedTicks / (double)System.Diagnostics.Stopwatch.Frequency * 1000;

            if (frameTimeRough > 30) frameTimeRough = 30;
            frameTime = frameTime * 0.99 + frameTimeRough * 0.01;
        }

        private void TheRest()
        {
            //positions and headings 
            CalculatePositionHeading();

            //calculate lookahead at full speed, no sentence misses
            CalculateSectionLookAhead(toolPos.northing, toolPos.easting);

            //To prevent drawing high numbers of triangles, determine and test before drawing vertex
            sectionTriggerDistance = glm.Distance(pn.fix, prevSectionPos);

            //section on off and points, contour points
            if (sectionTriggerDistance > sectionTriggerStepDistance && isJobStarted)
            {
                AddSectionOrContourPathPoints();

                //grab fix and elevation
                if (isLogElevation) sbFix.Append(pn.fix.easting.ToString("0.00") + "," + pn.fix.northing.ToString("0.00") + ","
                                                    + pn.altitude.ToString("0.00") + ","
                                                    + pn.latitude + "," + pn.longitude + "\r\n");
            }

            //test if travelled far enough for new boundary point
            if (bnd.isOkToAddPoints)
            {
                double boundaryDistance = glm.Distance(pn.fix, prevBoundaryPos);
                if (boundaryDistance > 1) AddBoundaryPoint();
            }

            //calc distance travelled since last GPS fix
            if (pn.speed > 1)
            {
                if ((fd.distanceUser += distanceCurrentStepFix) > 3000) fd.distanceUser = 0; ;//userDistance can be reset
            }
        }


        //all the hitch, pivot, section, trailing hitch, headings and fixes
        private void CalculatePositionHeading()
        {
            #region pivot hitch trail

            //translate from pivot position to steer axle and pivot axle position
            //translate world to the pivot axle
            pivotAxlePos.easting = pn.fix.easting - (Math.Sin(fixHeading) * vehicle.antennaPivot);
            pivotAxlePos.northing = pn.fix.northing - (Math.Cos(fixHeading) * vehicle.antennaPivot);

            steerAxlePos.easting = pivotAxlePos.easting + (Math.Sin(fixHeading) * vehicle.wheelbase);
            steerAxlePos.northing = pivotAxlePos.northing + (Math.Cos(fixHeading) * vehicle.wheelbase);

            //determine where the rigid vehicle hitch ends
            hitchPos.easting = pn.fix.easting + (Math.Sin(fixHeading) * (tool.hitchLength - vehicle.antennaPivot));
            hitchPos.northing = pn.fix.northing + (Math.Cos(fixHeading) * (tool.hitchLength - vehicle.antennaPivot));

            //tool attached via a trailing hitch
            if (tool.isToolTrailing)
            {
                double over;
                if (tool.isToolTBT)
                {
                    //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                    if (distanceCurrentStepFix != 0)
                    {
                        double t = (tool.toolTankTrailingHitchLength) / distanceCurrentStepFix;
                        tankPos.easting = hitchPos.easting + t * (hitchPos.easting - tankPos.easting);
                        tankPos.northing = hitchPos.northing + t * (hitchPos.northing - tankPos.northing);
                        tankPos.heading = Math.Atan2(hitchPos.easting - tankPos.easting, hitchPos.northing - tankPos.northing);
                    }

                    ////the tool is seriously jacknifed or just starting out so just spring it back.
                    over = Math.Abs(Math.PI - Math.Abs(Math.Abs(tankPos.heading - fixHeading) - Math.PI));

                    if (over < 2.0 && startCounter > 50)
                    {
                        tankPos.easting = hitchPos.easting + (Math.Sin(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                        tankPos.northing = hitchPos.northing + (Math.Cos(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                    }

                    //criteria for a forced reset to put tool directly behind vehicle
                    if (over > 2.0 | startCounter < 51)
                    {
                        tankPos.heading = fixHeading;
                        tankPos.easting = hitchPos.easting + (Math.Sin(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                        tankPos.northing = hitchPos.northing + (Math.Cos(tankPos.heading) * (tool.toolTankTrailingHitchLength));
                    }
                }
                else
                {
                    tankPos.heading = fixHeading;
                    tankPos.easting = hitchPos.easting;
                    tankPos.northing = hitchPos.northing;
                }

                //Torriem rules!!!!! Oh yes, this is all his. Thank-you
                if (distanceCurrentStepFix != 0)
                {
                    double t = (tool.toolTrailingHitchLength) / distanceCurrentStepFix;
                    toolPos.easting = tankPos.easting + t * (tankPos.easting - toolPos.easting);
                    toolPos.northing = tankPos.northing + t * (tankPos.northing - toolPos.northing);
                    toolPos.heading = Math.Atan2(tankPos.easting - toolPos.easting, tankPos.northing - toolPos.northing);
                }

                ////the tool is seriously jacknifed or just starting out so just spring it back.
                over = Math.Abs(Math.PI - Math.Abs(Math.Abs(toolPos.heading - tankPos.heading) - Math.PI));

                if (over < 1.9 && startCounter > 50)
                {
                    toolPos.easting = tankPos.easting + (Math.Sin(toolPos.heading) * (tool.toolTrailingHitchLength));
                    toolPos.northing = tankPos.northing + (Math.Cos(toolPos.heading) * (tool.toolTrailingHitchLength));
                }

                //criteria for a forced reset to put tool directly behind vehicle
                if (over > 1.9 | startCounter < 51)
                {
                    toolPos.heading = tankPos.heading;
                    toolPos.easting = tankPos.easting + (Math.Sin(toolPos.heading) * (tool.toolTrailingHitchLength));
                    toolPos.northing = tankPos.northing + (Math.Cos(toolPos.heading) * (tool.toolTrailingHitchLength));
                }
            }

            //rigidly connected to vehicle
            else
            {
                toolPos.heading = fixHeading;
                toolPos.easting = hitchPos.easting;
                toolPos.northing = hitchPos.northing;
            }

            #endregion

            //used to increase triangle count when going around corners, less on straight
            //pick the slow moving side edge of tool
            double distance = tool.toolWidth * 0.5;
            if (distance > 3) distance = 3;

            //whichever is less
            if (tool.toolFarLeftSpeed < tool.toolFarRightSpeed)
            {
                double twist = tool.toolFarLeftSpeed / tool.toolFarRightSpeed;
                //twist *= twist;
                if (twist < 0.2) twist = 0.2;
                sectionTriggerStepDistance = distance * twist * twist;
            }
            else
            {
                double twist = tool.toolFarRightSpeed / tool.toolFarLeftSpeed;
                //twist *= twist;
                if (twist < 0.2) twist = 0.2;

                sectionTriggerStepDistance = distance * twist * twist;
            }

            //finally fixed distance for making a curve line
            if (!gyd.isOkToAddDesPoints) sectionTriggerStepDistance = sectionTriggerStepDistance + 0.2;
            if (gyd.CurrentGMode == Mode.Contour) sectionTriggerStepDistance *= 0.5;

        }

        //perimeter and boundary point generation
        public void AddBoundaryPoint()
        {
            //save the north & east as previous
            prevBoundaryPos.easting = pn.fix.easting;
            prevBoundaryPos.northing = pn.fix.northing;

            //build the boundary line
            bnd.bndBeingMadePts.Add(new vec2(
                pivotAxlePos.easting + (Math.Cos(fixHeading) * (bnd.isDrawRightSide ? bnd.createBndOffset : -bnd.createBndOffset)),
                pivotAxlePos.northing - (Math.Sin(fixHeading) * (bnd.isDrawRightSide ? bnd.createBndOffset : -bnd.createBndOffset))));
        }

        //add the points for section, contour line points, Area Calc feature
        private void AddSectionOrContourPathPoints()
        {
            if (gyd.isRecordOn)
            {
                //keep minimum speed of 1.0
                double speed = pn.speed;
                if (pn.speed < 1.0) speed = 1.0;
                bool autoBtn = (autoBtnState == btnStates.Auto);

                gyd.recList.Add(new CRecPathPt(pivotAxlePos.easting, pivotAxlePos.northing, fixHeading, speed, autoBtn));
            }

            if (gyd.isOkToAddDesPoints && gyd.EditGuidanceLine != null)
            {
                gyd.EditGuidanceLine.curvePts.Add(new vec3(pivotAxlePos.easting, pivotAxlePos.northing, fixHeading));
            }

            //save the north & east as previous
            prevSectionPos.northing = pn.fix.northing;
            prevSectionPos.easting = pn.fix.easting;

            // if non zero, at least one section is on.
            int sectionCounter = 0;

            //send the current and previous GPS fore/aft corrected fix to each section
            for (int j = 0; j < tool.numOfSections + 1; j++)
            {
                if (section[j].isMappingOn)
                {
                    section[j].AddMappingPoint();
                    sectionCounter++;
                }
            }
            if (sectionCounter > 0 && (!isAutoSteerBtnOn || gyd.CurrentGMode == Mode.Contour))
                gyd.AddPoint(new vec3(pivotAxlePos.easting, pivotAxlePos.northing, fixHeading));
            else
                gyd.StopContourLine();
        }

        //calculate the extreme tool left, right velocities, each section lookahead, and whether or not its going backwards
        public void CalculateSectionLookAhead(double northing, double easting)
        {
            //precalc the sin and cos of heading * -1
            sinSectionHeading = Math.Sin(-toolPos.heading);
            cosSectionHeading = Math.Cos(-toolPos.heading);

            //calculate left side of section 1
            vec2 left = new vec2();
            vec2 right = left;
            double leftSpeed = 0, rightSpeed = 0;

            //speed max for section kmh*0.277 to m/s * 10 cm per pixel * 1.7 max speed
            double meterPerSecPerPixel = Math.Abs(pn.speed) * 4.5;

            //now loop all the section rights and the one extreme left
            for (int j = 0; j < tool.numOfSections; j++)
            {
                if (j == 0)
                {
                    vec2 lastLeftPoint = section[j].leftPoint;
                    //only one first left point, the rest are all rights moved over to left
                    section[j].leftPoint = new vec2(cosSectionHeading * (section[j].positionLeft) + easting,
                                       sinSectionHeading * (section[j].positionLeft) + northing);

                    left = section[j].leftPoint - lastLeftPoint;

                    //get the speed for left side only once
                    leftSpeed = left.GetLength() / fixUpdateTime;
                    if (leftSpeed > 27.7778) leftSpeed = 27.7778;

                    //Is section outer going forward or backward
                    if (Math.PI - Math.Abs(Math.Abs(left.HeadingXZ() - toolPos.heading) - Math.PI) > glm.PIBy2)
                    {
                        if (leftSpeed > 0) leftSpeed *= -1;
                    }
                    tool.toolFarLeftSpeed = tool.toolFarLeftSpeed * 0.9 + leftSpeed * 0.1;
                }
                else
                {
                    section[j].leftPoint = section[j - 1].rightPoint;
                    leftSpeed = rightSpeed;
                }

                vec2 lastRightPoint = section[j].rightPoint;

                section[j].rightPoint = new vec2(cosSectionHeading * (section[j].positionRight) + easting,
                                    sinSectionHeading * (section[j].positionRight) + northing);

                //now we have left and right for this section
                right = section[j].rightPoint - lastRightPoint;

                //grab vector length and convert to meters/sec/10 pixels per meter                
                rightSpeed = right.GetLength() / fixUpdateTime;
                if (rightSpeed > 27.7778) rightSpeed = 27.7778;

                if (Math.PI - Math.Abs(Math.Abs(right.HeadingXZ() - toolPos.heading) - Math.PI) > glm.PIBy2)
                    if (rightSpeed > 0) rightSpeed *= -1;

                //save the far left and right speed in m/sec averaged over 20%
                if (j == tool.numOfSections - 1)
                    tool.toolFarRightSpeed = tool.toolFarRightSpeed * 0.9 + rightSpeed * 0.1;

                double sped = (leftSpeed > rightSpeed) ? leftSpeed : rightSpeed; //choose fastest speed
                section[j].speedPixels = section[j].speedPixels * 0.9 + sped;
            }

            //fill in tool positions
            section[tool.numOfSections].leftPoint = section[0].leftPoint;
            section[tool.numOfSections].rightPoint = section[tool.numOfSections - 1].rightPoint;

            double oneFrameLeft = tool.toolFarLeftSpeed * fixUpdateTime * 10;
            double oneFrameRight = tool.toolFarRightSpeed * fixUpdateTime * 10;

            if (!isFastSections)
            {
                oneFrameLeft *= 2;
                oneFrameRight *= 2;
            }

            //set the look ahead for hyd Lift in pixels per second
            vehicle.hydLiftLookAheadDistanceLeft = oneFrameLeft + tool.toolFarLeftSpeed * vehicle.hydLiftLookAheadTime * 10;
            vehicle.hydLiftLookAheadDistanceRight = oneFrameRight + tool.toolFarRightSpeed * vehicle.hydLiftLookAheadTime * 10;

            if (vehicle.hydLiftLookAheadDistanceLeft > 250) vehicle.hydLiftLookAheadDistanceLeft = 250;
            else if (vehicle.hydLiftLookAheadDistanceLeft < -250) vehicle.hydLiftLookAheadDistanceLeft = -250;
            if (vehicle.hydLiftLookAheadDistanceRight > 250) vehicle.hydLiftLookAheadDistanceRight = 250;
            else if (vehicle.hydLiftLookAheadDistanceRight < -250) vehicle.hydLiftLookAheadDistanceRight = -250;

            double oneFrameLeftBoundary = oneFrameLeft * ((tool.boundOverlap - 50.0) * 0.02);
            double oneFrameRightBoundary = oneFrameRight * ((tool.boundOverlap - 50.0) * 0.02);

            oneFrameLeft *= (tool.minOverlap - 50.0) * 0.02;
            oneFrameRight *= (tool.minOverlap - 50.0) * 0.02;

            tool.lookAheadBoundaryOnPixelsLeft = Math.Max(oneFrameLeftBoundary, 0) + tool.toolFarLeftSpeed * tool.lookAheadOnSetting * 10;
            tool.lookAheadBoundaryOnPixelsRight = Math.Max(oneFrameRightBoundary, 0) + tool.toolFarRightSpeed * tool.lookAheadOnSetting * 10;

            tool.lookAheadBoundaryOffPixelsLeft = Math.Max(-oneFrameLeftBoundary, 0) + tool.toolFarLeftSpeed * tool.lookAheadOffSetting * 10;
            tool.lookAheadBoundaryOffPixelsRight = Math.Max(-oneFrameRightBoundary, 0) + tool.toolFarRightSpeed * tool.lookAheadOffSetting * 10;

            tool.lookAheadDistanceOnPixelsLeft = Math.Max(oneFrameLeft, 0) + tool.toolFarLeftSpeed * tool.lookAheadOnSetting * 10;
            tool.lookAheadDistanceOnPixelsRight = Math.Max(oneFrameRight, 0) + tool.toolFarRightSpeed * tool.lookAheadOnSetting * 10;

            tool.lookAheadDistanceOffPixelsLeft = Math.Max(-oneFrameLeft, 0) + tool.toolFarLeftSpeed * tool.lookAheadOffSetting * 10;
            tool.lookAheadDistanceOffPixelsRight = Math.Max(-oneFrameRight, 0) + tool.toolFarRightSpeed * tool.lookAheadOffSetting * 10;

            double maxLookAhead = 250;


            if (tool.lookAheadBoundaryOnPixelsLeft > maxLookAhead)
            {
                tool.lookAheadBoundaryOnPixelsLeft = maxLookAhead;
                if (tool.lookAheadBoundaryOffPixelsLeft > maxLookAhead)
                    tool.lookAheadBoundaryOffPixelsLeft = maxLookAhead;
            }
            else if (tool.lookAheadBoundaryOnPixelsLeft < -maxLookAhead)
            {
                tool.lookAheadBoundaryOnPixelsLeft = -maxLookAhead;
                if (tool.lookAheadBoundaryOffPixelsLeft < -maxLookAhead)
                    tool.lookAheadBoundaryOffPixelsLeft = -maxLookAhead;
            }

            if (tool.lookAheadBoundaryOnPixelsRight > maxLookAhead)
            {
                tool.lookAheadBoundaryOnPixelsRight = maxLookAhead;
                if (tool.lookAheadBoundaryOffPixelsRight > maxLookAhead)
                    tool.lookAheadBoundaryOffPixelsRight = maxLookAhead;
            }
            else if (tool.lookAheadBoundaryOnPixelsRight < -maxLookAhead)
            {
                tool.lookAheadBoundaryOnPixelsRight = -maxLookAhead;
                if (tool.lookAheadBoundaryOffPixelsRight < -maxLookAhead)
                    tool.lookAheadBoundaryOffPixelsRight = -maxLookAhead;
            }

            if (tool.lookAheadDistanceOnPixelsLeft > maxLookAhead)
            {
                tool.lookAheadDistanceOnPixelsLeft = maxLookAhead;
                if (tool.lookAheadDistanceOffPixelsLeft > maxLookAhead)
                    tool.lookAheadDistanceOffPixelsLeft = maxLookAhead;
            }
            else if (tool.lookAheadDistanceOnPixelsLeft < -maxLookAhead)
            {
                tool.lookAheadDistanceOnPixelsLeft = -maxLookAhead;
                if (tool.lookAheadDistanceOffPixelsLeft < -maxLookAhead)
                    tool.lookAheadDistanceOffPixelsLeft = -maxLookAhead;
            }

            if (tool.lookAheadDistanceOnPixelsRight > maxLookAhead)
            {
                tool.lookAheadDistanceOnPixelsRight = maxLookAhead;
                if (tool.lookAheadDistanceOffPixelsRight > maxLookAhead)
                    tool.lookAheadDistanceOffPixelsRight = maxLookAhead;
            }
            else if (tool.lookAheadDistanceOnPixelsRight < -maxLookAhead)
            {
                tool.lookAheadDistanceOnPixelsRight = -maxLookAhead;
                if (tool.lookAheadDistanceOffPixelsRight < -maxLookAhead)
                    tool.lookAheadDistanceOffPixelsRight = -maxLookAhead;
            }
        }

        //the start of first few frames to initialize entire program
        private void InitializeFirstFewGPSPositions()
        {
            if (!isFirstFixPositionSet)
            {
                if (!isJobStarted)
                {
                    if (timerSim.Enabled)
                    {
                        pn.latStart = Properties.Settings.Default.setGPS_SimLatitude;
                        pn.lonStart = Properties.Settings.Default.setGPS_SimLongitude;
                        sim.resetSim();
                    }
                    else
                    {
                        pn.latStart = pn.latitude;
                        pn.lonStart = pn.longitude;
                    }
                    pn.SetLocalMetersPerDegree();
                }

                pn.ConvertWGS84ToLocal(pn.latitude, pn.longitude, out pn.fix.northing, out pn.fix.easting);

                //Draw a grid once we know where in the world we are.
                isFirstFixPositionSet = true;
            }

            else
            {
                //keep here till valid data
                if (startCounter > (20))
                {
                    isGPSPositionInitialized = true;
                    dayNightCounter = 601;
                }
                else
                    dayNightCounter = 1;

                toolPos.heading = fixHeading = 0;
            }
            lastReverseFix = prevFix = pn.fix;
        }
    }//end class
}//end namespace