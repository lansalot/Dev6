﻿using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;

namespace AgOpenGPS
{
    public partial class CGuidance
    {
        /// <summary>/// triggered right after youTurnTriggerPoint is set /// </summary>
        public bool isYouTurnTriggered;

        /// <summary>  /// turning right or left?/// </summary>
        public bool isYouTurnRight;

        /// <summary> /// Is the youturn button enabled? /// </summary>
        public bool isYouTurnBtnOn;

        public double boundaryAngleOffPerpendicular;

        public int rowSkipsWidth = 1, uTurnSmoothing = 10;

        public bool alternateSkips = false, previousBigSkip = true;
        public int rowSkipsWidth2 = 3, turnSkips = 2;

        /// <summary>  /// distance from headland as offset where to start turn shape /// </summary>
        public int youTurnStartOffset;

        //guidance values
        public double distanceFromCurrentLine, uturnDistanceFromBoundary;

        public bool isTurnCreationTooClose = false, isTurnCreationNotCrossingError = false;

        //list of points for scaled and rotated YouTurn line, used for pattern, dubins, abcurve, abline
        public List<vec3> ytList = new List<vec3>();

        //is UTurn pattern in or out of bounds
        public bool isOutOfBounds = false;

        //sequence of operations of finding the next turn 0 to 3
        public int youTurnPhase, onA;

        public vec4 crossingCurvePoint = new vec4();

        //Finds the point where an AB Curve crosses the turn line
        public bool FindCurveTurnPoints()
        {

            crossingCurvePoint.easting = -20000;
            //find closet AB Curve point that will cross and go out of bounds
            int Count = isHeadingSameWay ? 1 : -1;
            int turnNum = 99;

            for (int j = currentLocationIndex; j > 0 && j < curList.Count; j += Count)
            {
                int idx = mf.bnd.IsPointInsideTurnArea(curList[j]);
                if (idx != 0)
                {
                    crossingCurvePoint.easting = curList[j - Count].easting;
                    crossingCurvePoint.northing = curList[j - Count].northing;
                    crossingCurvePoint.heading = curList[j - Count].heading;
                    crossingCurvePoint.index = j - Count;
                    turnNum = idx;
                    break;
                }
            }

            if (turnNum < 0)
                turnNum = 0;
            else if (turnNum == 99)
            {
                isTurnCreationNotCrossingError = true;
                return false;
            }

            int curTurnLineCount = mf.bnd.bndList[turnNum].turnLine.points.Count;

            //possible points close to AB Curve point
            List<int> turnLineCloseList = new List<int>();

            for (int j = 0; j < curTurnLineCount; j++)
            {
                if ((mf.bnd.bndList[turnNum].turnLine.points[j].easting - crossingCurvePoint.easting) < 15
                    && (mf.bnd.bndList[turnNum].turnLine.points[j].easting - crossingCurvePoint.easting) > -15
                    && (mf.bnd.bndList[turnNum].turnLine.points[j].northing - crossingCurvePoint.northing) < 15
                    && (mf.bnd.bndList[turnNum].turnLine.points[j].northing - crossingCurvePoint.northing) > -15)
                {
                    turnLineCloseList.Add(j);
                }
            }

            double dist1, dist2 = 99;
            curTurnLineCount = turnLineCloseList.Count;
            for (int i = 0; i < curTurnLineCount; i++)
            {
                dist1 = glm.Distance(mf.bnd.bndList[turnNum].turnLine.points[turnLineCloseList[i]].easting,
                                        mf.bnd.bndList[turnNum].turnLine.points[turnLineCloseList[i]].northing,
                                            crossingCurvePoint.easting, crossingCurvePoint.northing);
                if (dist1 < dist2)
                {
                    dist2 = dist1;
                }
            }

            return crossingCurvePoint.easting != -20000;
        }

        public void AddSequenceLines(double head)
        {
            vec3 pt;
            for (int a = 0; a < youTurnStartOffset * 2; a++)
            {
                pt.easting = ytList[0].easting + (Math.Sin(head) * 0.2);
                pt.northing = ytList[0].northing + (Math.Cos(head) * 0.2);
                pt.heading = ytList[0].heading;
                ytList.Insert(0, pt);
            }

            int count = ytList.Count;

            for (int i = 1; i <= youTurnStartOffset * 2; i++)
            {
                pt.easting = ytList[count - 1].easting + (Math.Sin(head) * i * 0.2);
                pt.northing = ytList[count - 1].northing + (Math.Cos(head) * i * 0.2);
                pt.heading = head;
                ytList.Add(pt);
            }

            double distancePivotToTurnLine;
            count = ytList.Count;
            for (int i = 0; i < count; i += 2)
            {
                distancePivotToTurnLine = glm.Distance(ytList[i], mf.pivotAxlePos);
                if (distancePivotToTurnLine > 3)
                {
                    isTurnCreationTooClose = false;
                }
                else
                {
                    isTurnCreationTooClose = true;
                    //set the flag to Critical stop machine
                    if (isTurnCreationTooClose) mf.mc.isOutOfBounds = true;
                    break;
                }
            }
        }

        ////list of points of collision path avoidance
        //public List<vec3> mazeList = new List<vec3>();

        //public bool BuildDriveAround()
        //{
        //    double headAB = mf.ABLine.abHeading;
        //    if (!mf.ABLine.isABSameAsVehicleHeading) headAB += Math.PI;

        //    double cosHead = Math.Cos(headAB);
        //    double sinHead = Math.Sin(headAB);

        //    vec3 start = new vec3();
        //    vec3 stop = new vec3();
        //    vec3 pt2 = new vec3();

        //    //grab the pure pursuit point right on ABLine
        //    vec3 onPurePoint = new vec3(mf.ABLine.rEastAB, mf.ABLine.rNorthAB, 0);

        //    //how far are we from any geoFence
        //    mf.gf.FindPointsDriveAround(onPurePoint, headAB, ref start, ref stop);

        //    //not an inside border
        //    if (start.easting == 88888) return false;

        //    //get the dubins path vec3 point coordinates of path
        //    ytList?.Clear();

        //    //find a path from start to goal - diagnostic, but also used later
        //    mazeList = mf.mazeGrid.SearchForPath(start, stop);

        //    //you can't get anywhere!
        //    if (mazeList == null) return false;

        //    //not really changing direction so need to fake a turn twice.
        //    mf.SwapDirection();

        //    //list of vec3 points of Dubins shortest path between 2 points - To be converted to RecPt
        //    List<vec3> shortestDubinsList = new List<vec3>();

        //    //Dubins at the start and stop of mazePath
        //    CDubins.turningRadius = mf.vehicle.minTurningRadius * 1.0;
        //    CDubins dubPath = new CDubins();

        //    //start is navigateable - maybe
        //    int cnt = mazeList.Count;
        //    int cut = 8;
        //    if (cnt < 18) cut = 3;

        //    if (cnt > 0)
        //    {
        //        pt2.easting = start.easting - (sinHead * mf.vehicle.minTurningRadius * 1.5);
        //        pt2.northing = start.northing - (cosHead * mf.vehicle.minTurningRadius * 1.5);
        //        pt2.heading = headAB;

        //        shortestDubinsList = dubPath.GenerateDubins(pt2, mazeList[cut - 1], mf.gf);
        //        for (int i = 1; i < shortestDubinsList.Count; i++)
        //        {
        //            vec3 pt = new vec3(shortestDubinsList[i].easting, shortestDubinsList[i].northing, shortestDubinsList[i].heading);
        //            ytList.Add(pt);
        //        }

        //        for (int i = cut; i < mazeList.Count - cut; i++)
        //        {
        //            vec3 pt = new vec3(mazeList[i].easting, mazeList[i].northing, mazeList[i].heading);
        //            ytList.Add(pt);
        //        }

        //        pt2.easting = stop.easting + (sinHead * mf.vehicle.minTurningRadius * 1.5);
        //        pt2.northing = stop.northing + (cosHead * mf.vehicle.minTurningRadius * 1.5);
        //        pt2.heading = headAB;

        //        shortestDubinsList = dubPath.GenerateDubins(mazeList[cnt - cut], pt2, mf.gf);

        //        for (int i = 1; i < shortestDubinsList.Count; i++)
        //        {
        //            vec3 pt = new vec3(shortestDubinsList[i].easting, shortestDubinsList[i].northing, shortestDubinsList[i].heading);
        //            ytList.Add(pt);
        //        }
        //    }

        //    if (ytList.Count > 10) youTurnPhase = 3;

        //    vec3 pt3 = new vec3();

        //    for (int a = 0; a < youTurnStartOffset; a++)
        //    {
        //        pt3.easting = ytList[0].easting - sinHead;
        //        pt3.northing = ytList[0].northing - cosHead;
        //        pt3.heading = headAB;
        //        ytList.Insert(0, pt3);
        //    }

        //    int count = ytList.Count;

        //    for (int i = 1; i <= youTurnStartOffset; i++)
        //    {
        //        pt3.easting = ytList[count - 1].easting + (sinHead * i);
        //        pt3.northing = ytList[count - 1].northing + (cosHead * i);
        //        pt3.heading = headAB;
        //        ytList.Add(pt3);
        //    }

        //    return true;
        //}

        public bool BuildABLineDubinsYouTurn(bool isTurnRight)
        {
            double headAB = abHeading;
            if (!isHeadingSameWay) headAB += Math.PI;

            if (youTurnPhase == 0)
            {
                //if (BuildDriveAround()) return true;

                //grab the pure pursuit point right on ABLine
                vec3 onPurePoint = new vec3(rEast, rNorth, 0);

                //how far are we from any turn boundary
                mf.bnd.FindClosestTurnPoint(isYouTurnRight, onPurePoint, headAB);

                //or did we lose the turnLine - we are on the highway cuz we left the outer/inner turn boundary
                if ((int)mf.bnd.closestTurnPt.easting != -20000)
                {
                    mf.distancePivotToTurnLine = glm.Distance(mf.pivotAxlePos, mf.bnd.closestTurnPt);
                }
                else
                {
                    //Full emergency stop code goes here, it thinks its auto turn, but its not!
                    mf.distancePivotToTurnLine = -3333;
                }

                //delta between AB heading and boundary closest point heading
                double boundaryAngleOffPerpendicular = 0;
                if (boundaryAngleOffPerpendicular > 1.25) boundaryAngleOffPerpendicular = 1.25;
                if (boundaryAngleOffPerpendicular < -1.25) boundaryAngleOffPerpendicular = -1.25;

                //for calculating innner circles of turn
                double tangencyAngle = (glm.PIBy2 - Math.Abs(boundaryAngleOffPerpendicular)) * 0.5;

                //baseline away from boundary to start calculations
                double toolTurnWidth = mf.tool.toolWidth * rowSkipsWidth;

                //distance from TurnLine for trigger added in youturn form, include the 3 m bump forward
                double distanceTurnBeforeLine = 0;

                if (mf.vehicle.minTurningRadius * 2 < toolTurnWidth)
                {
                    if (boundaryAngleOffPerpendicular < 0)
                    {
                        //which is actually left
                        if (isYouTurnRight)
                            distanceTurnBeforeLine += (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle));//short
                        else
                            distanceTurnBeforeLine += (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                    }
                    else
                    {
                        //which is actually left
                        if (isYouTurnRight)
                            distanceTurnBeforeLine += (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                        else
                            distanceTurnBeforeLine += (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle)); //short
                    }
                }
                else //turn Radius is wider then equipment width so ohmega turn
                {
                    distanceTurnBeforeLine += (2 * mf.vehicle.minTurningRadius);
                }

                //used for distance calc for other part of turn

                CDubins dubYouTurnPath = new CDubins();
                CDubins.turningRadius = mf.vehicle.minTurningRadius;

                //point on AB line closest to pivot axle point from ABLine PurePursuit
                double head = abHeading;

                //grab the vehicle widths and offsets
                double turnOffset = (mf.tool.toolWidth - mf.tool.toolOverlap) * rowSkipsWidth + (isYouTurnRight ? -mf.tool.toolOffset * 2.0 : mf.tool.toolOffset * 2.0);

                double turnRadius = turnOffset / Math.Cos(boundaryAngleOffPerpendicular);
                if (!isHeadingSameWay) head += Math.PI;

                //move the start forward 2 meters
                rEast += Math.Sin(head) * mf.distancePivotToTurnLine;
                rNorth += Math.Cos(head) * mf.distancePivotToTurnLine;

                vec3 start = new vec3(rEast, rNorth, head);
                vec3 goal = new vec3
                {
                    //move the cross line calc to not include first turn
                    easting = rEast + (Math.Sin(head) * distanceTurnBeforeLine),
                    northing = rNorth + (Math.Cos(head) * distanceTurnBeforeLine)
                };

                //headland angle relative to vehicle heading to head along the boundary left or right
                double bndAngle = head - boundaryAngleOffPerpendicular + glm.PIBy2;

                //now we go the other way to turn round
                head -= Math.PI;
                if (head < -Math.PI) head += glm.twoPI;
                if (head > Math.PI) head -= glm.twoPI;

                if ((mf.vehicle.minTurningRadius * 2.0) < turnOffset)
                {
                    //are we right of boundary
                    if (boundaryAngleOffPerpendicular > 0)
                    {
                        if (!isYouTurnRight) //which is actually right now
                        {
                            goal.easting += (Math.Sin(bndAngle) * turnRadius);
                            goal.northing += (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                        else //going left
                        {
                            goal.easting -= (Math.Sin(bndAngle) * turnRadius);
                            goal.northing -= (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle)); //short
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                    }
                    else // going left of boundary
                    {
                        if (!isYouTurnRight) //pointing to right
                        {
                            goal.easting += (Math.Sin(bndAngle) * turnRadius);
                            goal.northing += (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle)); //short
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                        else
                        {
                            goal.easting -= (Math.Sin(bndAngle) * turnRadius);
                            goal.northing -= (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                    }
                }
                else
                {
                    if (!isTurnRight)
                    {
                        goal.easting = rEast - (Math.Cos(-head) * turnOffset);
                        goal.northing = rNorth - (Math.Sin(-head) * turnOffset);
                    }
                    else
                    {
                        goal.easting = rEast + (Math.Cos(-head) * turnOffset);
                        goal.northing = rNorth + (Math.Sin(-head) * turnOffset);
                    }
                }

                goal.heading = head;

                //generate the turn points
                ytList = dubYouTurnPath.GenerateDubins(start, goal);
                AddSequenceLines(head);

                if (ytList.Count == 0) return false;
                else youTurnPhase = 1;
            }

            if (youTurnPhase == 3) return true;

            // Phase 0 - back up the turn till it is out of bounds.
            // Phase 1 - move it forward till out of bounds.
            // Phase 2 - move forward couple meters away from turn line.
            // Phase 3 - ytList is made, waiting to get close enough to it

            isOutOfBounds = false;
            switch (youTurnPhase)
            {
                case 1:
                    //the temp array
                    mf.distancePivotToTurnLine = glm.Distance(ytList[0], mf.pivotAxlePos);
                    double cosHead = Math.Cos(headAB);
                    double sinHead = Math.Sin(headAB);

                    int cnt = ytList.Count;
                    vec3[] arr2 = new vec3[cnt];

                    ytList.CopyTo(arr2);
                    ytList.Clear();

                    for (int i = 0; i < cnt; i++)
                    {
                        arr2[i].easting -= (sinHead);
                        arr2[i].northing -= (cosHead);
                        ytList.Add(arr2[i]);
                    }

                    for (int j = 0; j < cnt; j += 2)
                    {
                        if (mf.bnd.IsPointInsideTurnArea(ytList[j]) != 0)
                        {
                            isOutOfBounds = true;
                            break;
                        }
                    }

                    if (!isOutOfBounds)
                    {
                        youTurnPhase = 2;
                    }
                    else
                    {
                        //turn keeps approaching vehicle and running out of space - end of field?
                        if (isOutOfBounds && mf.distancePivotToTurnLine > 3)
                        {
                            isTurnCreationTooClose = false;
                        }
                        else
                        {
                            isTurnCreationTooClose = true;

                            //set the flag to Critical stop machine
                            if (isTurnCreationTooClose) mf.mc.isOutOfBounds = true;
                        }
                    }
                    break;

                //move again out of bounds
                case 2:
                    //the temp array
                    mf.distancePivotToTurnLine = glm.Distance(ytList[0], mf.pivotAxlePos);
                    cosHead = Math.Cos(headAB);
                    sinHead = Math.Sin(headAB);

                    cnt = ytList.Count;
                    vec3[] arr21 = new vec3[cnt];

                    ytList.CopyTo(arr21);
                    ytList.Clear();

                    for (int i = 0; i < cnt; i++)
                    {
                        arr21[i].easting += (sinHead * 0.05);
                        arr21[i].northing += (cosHead * 0.05);
                        ytList.Add(arr21[i]);
                    }

                    for (int j = 0; j < cnt; j += 2)
                    {
                        if (mf.bnd.IsPointInsideTurnArea(ytList[j]) != 0)
                        {
                            isOutOfBounds = true;
                            break;
                        }
                    }

                    if (isOutOfBounds)
                    {
                        isOutOfBounds = false;
                        youTurnPhase = 3;
                    }
                    else
                    {
                        //turn keeps approaching vehicle and running out of space - end of field?
                        if (!isOutOfBounds && mf.distancePivotToTurnLine > 3)
                        {
                            isTurnCreationTooClose = false;
                        }
                        else
                        {
                            isTurnCreationTooClose = true;

                            //set the flag to Critical stop machine
                            if (isTurnCreationTooClose) mf.mc.isOutOfBounds = true;
                        }
                    }
                    break;
            }
            return true;
        }

        public bool BuildCurveDubinsYouTurn(bool isTurnRight, vec3 pivotPos)
        {
            if (youTurnPhase > 0)
            {
                double head = crossingCurvePoint.heading;
                if (!isHeadingSameWay) head += Math.PI;

                //delta between AB heading and boundary closest point heading
                boundaryAngleOffPerpendicular = 0;
                boundaryAngleOffPerpendicular -= glm.PIBy2;
                boundaryAngleOffPerpendicular *= -1;
                if (boundaryAngleOffPerpendicular > 1.25) boundaryAngleOffPerpendicular = 1.25;
                if (boundaryAngleOffPerpendicular < -1.25) boundaryAngleOffPerpendicular = -1.25;

                //for calculating innner circles of turn
                double tangencyAngle = (glm.PIBy2 - Math.Abs(boundaryAngleOffPerpendicular)) * 0.5;

                double distanceTurnBeforeLine;
                //distance from crossPoint to turn line
                if (mf.vehicle.minTurningRadius * 2 < (mf.tool.toolWidth * rowSkipsWidth))
                {
                    if (boundaryAngleOffPerpendicular < 0)
                    {
                        //which is actually left
                        if (isYouTurnRight)
                            distanceTurnBeforeLine = (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle));//short
                        else
                            distanceTurnBeforeLine = (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                    }
                    else
                    {
                        //which is actually left
                        if (isYouTurnRight)
                            distanceTurnBeforeLine = (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                        else
                            distanceTurnBeforeLine = (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle)); //short
                    }
                }

                //turn Radius is wider then equipment width so ohmega turn
                else
                {
                    distanceTurnBeforeLine = (2 * mf.vehicle.minTurningRadius);
                }

                CDubins dubYouTurnPath = new CDubins();
                CDubins.turningRadius = mf.vehicle.minTurningRadius;

                //grab the vehicle widths and offsets
                double turnOffset = (mf.tool.toolWidth - mf.tool.toolOverlap) * rowSkipsWidth + (isYouTurnRight ? -mf.tool.toolOffset * 2.0 : mf.tool.toolOffset * 2.0);

                //diagonally across
                double turnRadius = turnOffset / Math.Cos(boundaryAngleOffPerpendicular);

                //start point of Dubins
                vec3 start = new vec3(crossingCurvePoint.easting, crossingCurvePoint.northing, head);

                vec3 goal = new vec3
                {
                    //move the cross line calc to not include first turn
                    easting = crossingCurvePoint.easting + (Math.Sin(head) * distanceTurnBeforeLine),
                    northing = crossingCurvePoint.northing + (Math.Cos(head) * distanceTurnBeforeLine)
                };

                //headland angle relative to vehicle heading to head along the boundary left or right
                double bndAngle = head - boundaryAngleOffPerpendicular + glm.PIBy2;

                //now we go the other way to turn round
                head -= Math.PI;
                if (head < -Math.PI) head += glm.twoPI;
                if (head > Math.PI) head -= glm.twoPI;

                if ((mf.vehicle.minTurningRadius * 2.0) < turnOffset)
                {
                    //are we right of boundary
                    if (boundaryAngleOffPerpendicular > 0)
                    {
                        if (!isYouTurnRight) //which is actually right now
                        {
                            goal.easting += (Math.Sin(bndAngle) * turnRadius);
                            goal.northing += (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                        else //going left
                        {
                            goal.easting -= (Math.Sin(bndAngle) * turnRadius);
                            goal.northing -= (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle)); //short
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                    }
                    else // going left of boundary
                    {
                        if (!isYouTurnRight) //pointing to right
                        {
                            goal.easting += (Math.Sin(bndAngle) * turnRadius);
                            goal.northing += (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius * Math.Tan(tangencyAngle)); //short
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                        else
                        {
                            goal.easting -= (Math.Sin(bndAngle) * turnRadius);
                            goal.northing -= (Math.Cos(bndAngle) * turnRadius);

                            double dis = (mf.vehicle.minTurningRadius / Math.Tan(tangencyAngle)); //long
                            goal.easting += (Math.Sin(head) * dis);
                            goal.northing += (Math.Cos(head) * dis);
                        }
                    }
                }
                else
                {
                    if (!isTurnRight)
                    {
                        goal.easting = crossingCurvePoint.easting - (Math.Cos(-head) * turnOffset);
                        goal.northing = crossingCurvePoint.northing - (Math.Sin(-head) * turnOffset);
                    }
                    else
                    {
                        goal.easting = crossingCurvePoint.easting + (Math.Cos(-head) * turnOffset);
                        goal.northing = crossingCurvePoint.northing + (Math.Sin(-head) * turnOffset);
                    }
                }

                goal.heading = head;

                //goal.easting += (Math.Sin(head) * 0.5);
                //goal.northing += (Math.Cos(head) * 0.5);
                //goal.heading = head;

                //generate the turn points
                ytList = dubYouTurnPath.GenerateDubins(start, goal);
                int count = ytList.Count;
                if (count == 0) return false;

                //these are the lead in lead out lines that add to the turn
                AddSequenceLines(head);
            }

            switch (youTurnPhase)
            {
                case 0: //find the crossing points
                    if (FindCurveTurnPoints()) youTurnPhase = 1;
                    ytList?.Clear();
                    break;

                case 1:
                    //now check to make sure we are not in an inner turn boundary - drive thru is ok
                    int count = ytList.Count;
                    if (count == 0) return false;

                    //Are we out of bounds?
                    isOutOfBounds = false;
                    for (int j = 0; j < count; j += 2)
                    {
                        if (mf.bnd.IsPointInsideTurnArea(ytList[j]) != 0)
                        {
                            isOutOfBounds = true;
                            break;
                        }
                    }

                    //first check if not out of bounds, add a bit more to clear turn line, set to phase 2
                    if (!isOutOfBounds)
                    {
                        youTurnPhase = 2;
                        //if (mf.curve.isABSameAsVehicleHeading)
                        //{
                        //    crossingCurvePoint.index -= 2;
                        //    if (crossingCurvePoint.index < 0) crossingCurvePoint.index = 0;
                        //}
                        //else
                        //{
                        //    crossingCurvePoint.index += 2;
                        //    if (crossingCurvePoint.index >= curListCount)
                        //        crossingCurvePoint.index = curListCount - 1;
                        //}
                        //crossingCurvePoint.easting = mf.curve.curList[crossingCurvePoint.index].easting;
                        //crossingCurvePoint.northing = mf.curve.curList[crossingCurvePoint.index].northing;
                        //crossingCurvePoint.heading = mf.curve.curList[crossingCurvePoint.index].heading;
                        return true;
                    }

                    //keep moving infield till pattern is all inside
                    if (isHeadingSameWay)
                    {
                        crossingCurvePoint.index--;
                        if (crossingCurvePoint.index < 0) crossingCurvePoint.index = 0;
                    }
                    else
                    {
                        crossingCurvePoint.index++;
                        if (crossingCurvePoint.index >= curList.Count)
                            crossingCurvePoint.index = curList.Count - 1;
                    }
                    crossingCurvePoint.easting = curList[crossingCurvePoint.index].easting;
                    crossingCurvePoint.northing = curList[crossingCurvePoint.index].northing;
                    crossingCurvePoint.heading = curList[crossingCurvePoint.index].heading;

                    double tooClose = glm.Distance(ytList[0], pivotPos);
                    isTurnCreationTooClose = tooClose < 3;

                    //set the flag to Critical stop machine
                    if (isTurnCreationTooClose) mf.mc.isOutOfBounds = true;
                    break;

                case 2:
                    youTurnPhase = 3;
                    break;
            }
            return true;
        }

        public void SmoothYouTurn(int smPts)
        {
            //count the reference list of original curve
            int cnt = ytList.Count;

            //the temp array
            vec3[] arr = new vec3[cnt];

            //read the points before and after the setpoint
            for (int s = 0; s < smPts / 2; s++)
            {
                arr[s].easting = ytList[s].easting;
                arr[s].northing = ytList[s].northing;
                arr[s].heading = ytList[s].heading;
            }

            for (int s = cnt - (smPts / 2); s < cnt; s++)
            {
                arr[s].easting = ytList[s].easting;
                arr[s].northing = ytList[s].northing;
                arr[s].heading = ytList[s].heading;
            }

            //average them - center weighted average
            for (int i = smPts / 2; i < cnt - (smPts / 2); i++)
            {
                for (int j = -smPts / 2; j < smPts / 2; j++)
                {
                    arr[i].easting += ytList[j + i].easting;
                    arr[i].northing += ytList[j + i].northing;
                }
                arr[i].easting /= smPts;
                arr[i].northing /= smPts;
                arr[i].heading = ytList[i].heading;
            }

            ytList?.Clear();

            //calculate new headings on smoothed line
            for (int i = 1; i < cnt - 1; i++)
            {
                arr[i].heading = Math.Atan2(arr[i + 1].easting - arr[i].easting, arr[i + 1].northing - arr[i].northing);
                if (arr[i].heading < 0) arr[i].heading += glm.twoPI;
                ytList.Add(arr[i]);
            }
        }

        //called to initiate turn
        public void YouTurnTrigger()
        {
            //trigger pulled
            isYouTurnTriggered = true;

            if (alternateSkips && rowSkipsWidth2 > 1)
            {
                if (--turnSkips == 0)
                {
                    isYouTurnRight = !isYouTurnRight;
                    turnSkips = rowSkipsWidth2 * 2 - 1;
                }
                else if (previousBigSkip = !previousBigSkip)
                    rowSkipsWidth = rowSkipsWidth2 - 1;
                else
                    rowSkipsWidth = rowSkipsWidth2;
            }
            else isYouTurnRight = !isYouTurnRight;

            mf.guidanceLookPos.easting = ytList[ytList.Count - 1].easting;
            mf.guidanceLookPos.northing = ytList[ytList.Count - 1].northing;

            if (isABLineSet)
            {
                isLateralTriggered = true;
                isABValid = false;
            }
            else
            {
                isLateralTriggered = true;
                isCurveValid = false;
            }
        }

        //Normal copmpletion of youturn
        public void CompleteYouTurn()
        {
            isYouTurnTriggered = false;
            ResetCreatedYouTurn();
            mf.sounds.isBoundAlarming = false;
        }

        public void Set_Alternate_skips()
        {
            rowSkipsWidth2 = rowSkipsWidth;
            turnSkips = rowSkipsWidth2 * 2 - 1;
            previousBigSkip = false;
        }

        //something went seriously wrong so reset everything
        public void ResetYouTurn()
        {
            //fix you turn
            isYouTurnTriggered = false;
            ytList?.Clear();
            ResetCreatedYouTurn();
            mf.sounds.isBoundAlarming = false;
            isTurnCreationTooClose = false;
            isTurnCreationNotCrossingError = false;
        }

        public void ResetCreatedYouTurn()
        {
            youTurnPhase = -2;
            ytList?.Clear();
        }

        public void BuildManualYouLateral(bool isTurnRight)
        {
            if (isABLineSet || isCurveSet)
            {
                double head = CurrentHeading;

                isLateralTriggered = true;

                //grab the vehicle widths and offsets
                double turnOffset = (mf.tool.toolWidth - mf.tool.toolOverlap); //remove rowSkips

                //if its straight across it makes 2 loops instead so goal is a little lower then start
                if (!isHeadingSameWay) head += Math.PI;

                //move the start forward 2 meters, this point is critical to formation of uturn
                rEast += (Math.Sin(head) * 2);
                rNorth += (Math.Cos(head) * 2);

                if (isTurnRight)
                {
                    mf.guidanceLookPos.easting = rEast + (Math.Cos(-head) * turnOffset);
                    mf.guidanceLookPos.northing = rNorth + (Math.Sin(-head) * turnOffset);
                }
                else
                {
                    mf.guidanceLookPos.easting = rEast - (Math.Cos(-head) * turnOffset);
                    mf.guidanceLookPos.northing = rNorth - (Math.Sin(-head) * turnOffset);
                }

                isABValid = false;
                isCurveValid = false;
            }
        }

        //build the points and path of youturn to be scaled and transformed
        public void BuildManualYouTurn(bool isTurnRight, bool isTurnButtonTriggered)
        {
            if (isABLineSet || isCurveSet)
            {
                double head = CurrentHeading;
                isLateralTriggered = true;

                //grab the vehicle widths and offsets
                double turnOffset = (mf.tool.toolWidth - mf.tool.toolOverlap) * rowSkipsWidth + (isTurnRight ? mf.tool.toolOffset * 2.0 : -mf.tool.toolOffset * 2.0);

                CDubins dubYouTurnPath = new CDubins();
                CDubins.turningRadius = mf.vehicle.minTurningRadius;

                //if its straight across it makes 2 loops instead so goal is a little lower then start
                if (!isHeadingSameWay) head += 3.14;
                else head -= 0.01;

                //move the start forward 2 meters, this point is critical to formation of uturn
                rEast += (Math.Sin(head) * 4);
                rNorth += (Math.Cos(head) * 4);

                //now we have our start point
                vec3 start = new vec3(rEast, rNorth, head);
                vec3 goal = new vec3();

                //now we go the other way to turn round
                head -= Math.PI;
                if (head < 0) head += glm.twoPI;

                //set up the goal point for Dubins
                goal.heading = head;
                if (isTurnButtonTriggered)
                {
                    if (isTurnRight)
                    {
                        goal.easting = rEast - (Math.Cos(-head) * turnOffset);
                        goal.northing = rNorth - (Math.Sin(-head) * turnOffset);
                    }
                    else
                    {
                        goal.easting = rEast + (Math.Cos(-head) * turnOffset);
                        goal.northing = rNorth + (Math.Sin(-head) * turnOffset);
                    }
                }

                //generate the turn points
                ytList = dubYouTurnPath.GenerateDubins(start, goal);

                mf.guidanceLookPos.easting = ytList[ytList.Count - 1].easting;
                mf.guidanceLookPos.northing = ytList[ytList.Count - 1].northing;

                //vec3 pt;
                //for (double a = 0; a < 2; a += 0.2)
                //{
                //    pt.easting = ytList[0].easting + (Math.Sin(head) * a);
                //    pt.northing = ytList[0].northing + (Math.Cos(head) * a);
                //    pt.heading = ytList[0].heading;
                //    ytList.Insert(0, pt);
                //}

                //int count = ytList.Count;

                //for (double i = 0.2; i <= 7; i += 0.2)
                //{
                //    pt.easting = ytList[count - 1].easting + (Math.Sin(head) * i);
                //    pt.northing = ytList[count - 1].northing + (Math.Cos(head) * i);
                //    pt.heading = head;
                //    ytList.Add(pt);
                //}


                isABValid = false;
                isCurveValid = false;
            }
        }

        //Duh.... What does this do....
        public void DrawYouTurn()
        {
            int ptCount = ytList.Count;
            if (ptCount < 3) return;
            GL.PointSize(lineWidth);

            if (isYouTurnTriggered)
                GL.Color3(0.95f, 0.5f, 0.95f);
            else if (isOutOfBounds)
                GL.Color3(0.9495f, 0.395f, 0.325f);
            else
                GL.Color3(0.395f, 0.925f, 0.30f);

            GL.Begin(PrimitiveType.Points);
            for (int i = 0; i < ptCount; i++)
            {
                GL.Vertex3(ytList[i].easting, ytList[i].northing, 0);
            }
            GL.End();
        }
    }
}