using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Windows.Media.Media3D;
using System.Timers;
using System;
using static petri.PgViewModel;
using System.Threading;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;
using Point = System.Windows.Point;
using System.Reflection.Metadata;
using System.Xaml;
using System.Xml.Linq;

namespace petri
{

    class PgViewModel : INotifyPropertyChanged
    {
        //https://stackoverflow.com/questions/30922050/fast-access-to-matrix-as-jagged-array-in-c-sharp

        //Drawing related
        static object timerLock = new object();
        public static int board = 500; //width and height of the game field
        public static int bytesperpixel = 4;
        public static int stride = board * bytesperpixel;
        public static byte[] imgdata = new byte[board * board * bytesperpixel];
        public event PropertyChangedEventHandler PropertyChanged;
        public static Stopwatch calcWatch = new Stopwatch();
        public static Stopwatch graphicsWatch = new Stopwatch();
        public static Stopwatch courseWatch = new Stopwatch();
        public static int monitorHits = 0;
        public static int rotatedHits = 0;
        public static int scanHits = 0;
        public static int targetFPS = 30;
        public static int lowestFPScalc = 1000000;
        public static int lowestFPSgraph = 1000000;
        public static BitmapSource currentPgImage = new WriteableBitmap(board, board, 96, 96, PixelFormats.Bgr32, null);
        public static int gridTopOffset = 0;
        public static int gridLeftOffset = 0;
        public static int ruleNumber = 0;
        public BitmapSource CurrentPgImage
        {
            get { return currentPgImage; }
            set
            {
                currentPgImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPgImage"));
            }
        }

        //Jagged array is faster than 2d array or a single array for my data size
        //Did I properly init static array for a continious memory allocation?
        public static bool gameStarted = false;
        public static Dot[][] dots = InitDots();

        public static List<LineOfVision> lineOfVisionList = new List<LineOfVision>();
        public static Dot testDot = new Dot();
        public static List<Dot> actorsInSight = new List<Dot>();
        public static List<Dot> actorsList = new List<Dot>();
        public static List<Dot> actorsToRetainList = new List<Dot>();
        public static List<Dot> referenceList = new List<Dot>();
        public static List<Dot> concatReferenceList = new List<Dot>();
        public static int scanDepth = 0;
        public static int eatFood = 0;

        public static int p1food = 0;
        public static int p2food = 0;

        public static int rulesNoHit = 0;


        public Random random = new Random();

        public static List<Dot> localNeighborEmptyList = new List<Dot>();
        public static List<Dot> neighborFoodList = new List<Dot>();
        public static List<Dot> neighborEmptyList = new List<Dot>();
        public static List<Dot> neighborAlliesList = new List<Dot>();
        public static List<Dot> neighborEnemyList = new List<Dot>();
        public static List<Dot> farNeighborFoodList = new List<Dot>();
        public static List<Dot> farNeighborEmptyList = new List<Dot>();
        public static List<Dot> farNeighborAlliesList = new List<Dot>();
        public static List<Dot> farNeighborEnemyList = new List<Dot>();

        public static System.Timers.Timer aTimer;
        public static DispatcherTimer gTimer = new DispatcherTimer();


        public static List<Rule> p1ruleset = new List<Rule>();
        public static List<Rule> p2ruleset = new List<Rule>();

        public static int[] p1ruleHit;
        public static int[] p2ruleHit;

        public static List<Rule> ruleset = new List<Rule>();
        public static List<(ComboBox, ComboBox, ComboBox, ComboBox)> p1rulesetReference = new List<(ComboBox, ComboBox, ComboBox, ComboBox)>();
        public static List<(ComboBox, ComboBox, ComboBox, ComboBox)> p2rulesetReference = new List<(ComboBox, ComboBox, ComboBox, ComboBox)>();
        public static List<Label> p1rulesetHits = new List<Label>();
        public static List<Label> p2rulesetHits = new List<Label>();
        public static int LCompare;
        public static int RCompare;


        //Replace whole "Food is near then eat" with behaviour ("if no enemies near - forage")

        public static List<(byte, string)> workerWhoChoices = new List<(byte, string)>
        {
            (0, "Food"),
            (1, "Ally"),
            (2, "Enemy"),
            (3, "Empty cell"),

        };
        public static List<(byte, string)> workerWhereChoices = new List<(byte, string)>
        {
            (0, "near"),
            (1, "far"),
            (2, "not seen")

        };
        public static List<(byte, string)> workerAndChoices = new List<(byte, string)>
        {
            (0, "<skip>"),
            (1, "allies >= enemies"),
            (2, "enemies >= allies"),
            (3, "empty >= enemies"),
            (4, "empty >= allies")



        };
        public static List<(byte, string)> workerThenChoices = new List<(byte, string)>
        {
            (0, "move to it"),
            (1, "move away"),
            (2, "eat"),
            (3, "roam"),
            (4, "do nothing"),
            (5, "stick to it")
        };
        public class Rule
        {
            public int playerID;
            public int who;
            public int where;
            public int and;
            public int then;
        }

        public struct LineOfVision
        {
            public short x;
            public short y;
        }

        public struct Dot
        {
            public byte playerID;
            public byte type; //0 - worker, 1 - warrior, 10 - resource, 11 empty
            public byte status; //0 - roaming, 1 - sees enemy,            
            public byte density;
            public byte moveSpeed;
            public byte carriedFood;
            public byte scanDepth;
            public short face_x;
            public short face_y;
            public short x;
            public short y;

        }



        //Place random dots to start the game
        public void InitPlaceSpawn()
        {
            short randomPosX;
            short randomPosY;

            for (var i = 0; i < 5000; i++)
            {
                randomPosX = (short)random.Next(40, 480);
                randomPosY = (short)random.Next(40, 480);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDot(1, randomPosX, randomPosY, 0, (short)(randomPosX - 1), (short)(randomPosY - 1));
                    actorsList.Add(dots[randomPosX][randomPosY]);
                    testDot = dots[randomPosX][randomPosY];
                }
            }

            for (var i = 0; i < 5000; i++)
            {
                randomPosX = (short)random.Next(50, 480);
                randomPosY = (short)random.Next(30, 480);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDot(2, randomPosX, randomPosY, 0, (short)(randomPosX - 1), (short)(randomPosY - 1));
                    actorsList.Add(dots[randomPosX][randomPosY]);
                }
            }
            for (var i = 0; i < 40000; i++)
            {
                randomPosX = (short)random.Next(50, 480);
                randomPosY = (short)random.Next(50, 480);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDot(100, randomPosX, randomPosY, 100, 0, 0);
                }
            }

        }


        public static Dot[][] InitDots()
        {
            //Init jagged array, set borders and coordinates

            Dot[][] result = new Dot[board][];

            for (short x = 0; x < board; ++x)
            {
                result[x] = new Dot[board];
                for (short y = 0; y < board; ++y)
                {
                    result[x][y] = new Dot();
                    result[x][y].x = x;
                    result[x][y].y = y;
                    if (x < 20 || x > board - 20 || y < 20 || y > board - 20)
                        result[x][y].playerID = 255;
                    else
                        result[x][y].playerID = 0;

                    imgdata[x * stride + y * 4 + 0] = 255;
                    imgdata[x * stride + y * 4 + 1] = 255;
                    imgdata[x * stride + y * 4 + 2] = 255;
                    imgdata[x * stride + y * 4 + 3] = 255;
                }
            }
            return result;
        }

        public void UpdateDot(byte playerID, short x, short y, byte type, short cameFromX, short cameFromY)
        {
            byte color;
            dots[x][y].type = type;
            dots[x][y].playerID = playerID;

            if (cameFromX != 0 && cameFromY != 0)
            {
                dots[x][y].face_x = (short)(x - cameFromX);
                dots[x][y].face_y = (short)(y - cameFromY);
            }

            if (playerID == 1)
            {
                color = 0x0000064;
                imgdata[x * stride + y * 4 + 0] = color;
                imgdata[x * stride + y * 4 + 1] = 0;
                imgdata[x * stride + y * 4 + 2] = 0;
                imgdata[x * stride + y * 4 + 3] = color;
            }
            else if (playerID == 2)
            {
                color = 0x0000064;
                imgdata[x * stride + y * 4 + 0] = 0;
                imgdata[x * stride + y * 4 + 1] = 0;
                imgdata[x * stride + y * 4 + 2] = color;
                imgdata[x * stride + y * 4 + 3] = color;
            }
            else if (playerID == 100)
            {
                imgdata[x * stride + y * 4 + 0] = 0;
                imgdata[x * stride + y * 4 + 1] = 0;
                imgdata[x * stride + y * 4 + 2] = 0;
                imgdata[x * stride + y * 4 + 3] = 255;
            }
            else
            {
                imgdata[x * stride + y * 4 + 0] = 0;
                imgdata[x * stride + y * 4 + 1] = 0;
                imgdata[x * stride + y * 4 + 2] = 0;
                imgdata[x * stride + y * 4 + 3] = 0;
            }
        }


        public void ColorDot(short x, short y, byte sample)
        {
            if (sample == 0)
            {
                byte color = 0x0000064;
                imgdata[x * stride + y * 4 + 0] = color;
                imgdata[x * stride + y * 4 + 1] = color;
                imgdata[x * stride + y * 4 + 2] = color;
                imgdata[x * stride + y * 4 + 3] = color;
            }
            else
            {
                byte color = 0x0000032;
                imgdata[x * stride + y * 4 + 0] = color;
                imgdata[x * stride + y * 4 + 1] = color;
                imgdata[x * stride + y * 4 + 2] = color;
                imgdata[x * stride + y * 4 + 3] = color;
            }
        }

        public void EraseDot(short x, short y)
        {
            dots[x][y].type = 0;
            dots[x][y].playerID = 0;
            dots[x][y].face_x = 0;
            dots[x][y].face_y = 0;
            imgdata[x * stride + y * 4 + 0] = 0;
            imgdata[x * stride + y * 4 + 1] = 0;
            imgdata[x * stride + y * 4 + 2] = 0;
            imgdata[x * stride + y * 4 + 3] = 0;

        }

        public void RotateDot(short x, short y)
        {
            if (dots[x][y].x == 20)
            {
                dots[x][y].face_x = 1;
                dots[x][y].face_y = 1;
            }
            else if (dots[x][y].y == 20)
            {
                dots[x][y].face_x = 0;
                dots[x][y].face_y = 1;

            }
            else if (dots[x][y].x == board - 20)
            {
                dots[x][y].face_x = -1;
                dots[x][y].face_y = -1;
            }
            else if (dots[x][y].y == board - 20)
            {
                dots[x][y].face_x = 0;
                dots[x][y].face_y = -1;
            }
            else
            {
                //They might turn the same direction they where facing...
                dots[x][y].face_x = (short)random.Next(-1, 2);
                dots[x][y].face_y = (short)random.Next(-1, 2);
            }

            rotatedHits++;

        }


        //There's no definitive answer to this question, but using unsafe code with pinned structures (what you call "pointers") can have performance benefits when, for example, you are looping through an array in a very tight loop, need the highest speed possible, but don't need bounds checking. Your mileage is going to vary depending on what you are doing in the loop, but I've seen 10% speed increases by simply declaring a method unsafe.
        public void ScanNeighbors(Dot actor)
        {
            neighborFoodList.Clear();
            neighborEmptyList.Clear();
            neighborAlliesList.Clear();
            neighborEnemyList.Clear();
            farNeighborFoodList.Clear();
            farNeighborEmptyList.Clear();
            farNeighborAlliesList.Clear();
            farNeighborEnemyList.Clear();

            actorsInSight.Clear();


            if ((actor.face_x == 1 && actor.face_y == 1) || (actor.face_x == 1 && actor.face_y == -1) || (actor.face_x == -1 && actor.face_y == 1) || (actor.face_x == -1 && actor.face_y == -1))
            {
                for (int x = 0 * actor.face_x; x != scanDepth * actor.face_x; x += (1 * actor.face_x))
                {
                    for (int y = 0 * actor.face_y; y != scanDepth * actor.face_y; y += (1 * actor.face_y))
                    {
                        if (x == 0 && y == 0)
                            continue;
                        actorsInSight.Add(dots[actor.x + x][actor.y + y]);
                    }
                }
            }
            else
            {
                for (int shortCycle = 0; shortCycle < scanDepth; shortCycle++)
                {
                    for (int longerCycle = shortCycle * -1; longerCycle < shortCycle + 1; longerCycle++)
                    {
                        if (shortCycle == 0 && longerCycle == 0)
                            continue;
                        if (actor.face_x == -1 && actor.face_y == 0)
                            actorsInSight.Add(dots[actor.x - shortCycle][actor.y + longerCycle]);
                        else if (actor.face_x == 1 && actor.face_y == 0)
                            actorsInSight.Add(dots[actor.x + shortCycle][actor.y + longerCycle]);
                        else if (actor.face_x == 0 && actor.face_y == 1)
                            actorsInSight.Add(dots[actor.x + longerCycle][actor.y + shortCycle]);
                        else if (actor.face_x == 0 && actor.face_y == -1)
                            actorsInSight.Add(dots[actor.x + longerCycle][actor.y - shortCycle]);
                    }
                }

            }





            foreach (Dot actorInSight in actorsInSight)
            {

                if (actorInSight.x == actor.x && actorInSight.y == actor.y)
                {
                    continue;
                }
                else if (actorInSight.playerID == 255)
                {
                    continue;
                }
                if (actorInSight.x >= actor.x - 1 && actorInSight.y >= actor.y - 1 && actorInSight.x <= actor.x + 1 && actorInSight.y <= actor.y + 1)
                {
                    if (actorInSight.playerID == 100)
                    {
                        neighborFoodList.Add(actorInSight);
                    }
                    else if (actorInSight.playerID == 0)
                    {
                        neighborEmptyList.Add(actorInSight);
                    }
                    else if (actorInSight.playerID == actor.playerID)
                    {
                        neighborAlliesList.Add(actorInSight);
                    }
                    else if (actorInSight.playerID != actor.playerID)
                    {
                        neighborEnemyList.Add(actorInSight);
                    }
                }
                else if (neighborEmptyList.Count != 0)
                {
                    if (actorInSight.playerID == 100)
                    {
                        farNeighborFoodList.Add(actorInSight);
                    }
                    else if (actorInSight.playerID == 0)
                    {
                        farNeighborEmptyList.Add(actorInSight);
                    }
                    else if (actorInSight.playerID == actor.playerID)
                    {
                        farNeighborAlliesList.Add(actorInSight);
                    }
                    else if (actorInSight.playerID != actor.playerID)
                    {
                        farNeighborEnemyList.Add(actorInSight);
                    }
                }
            }


        }




        public Dot GetNearestDotFromFarTarget(Dot actor, List<Dot> targetList)
        {
            byte r = (byte)random.Next(targetList.Count);
            Dot t = targetList[r];

            int deltaX = (int)(t.x - actor.x);
            int deltaY = (int)(t.y - actor.y);
            int delta = (deltaX * deltaX) + (deltaY * deltaY);

            foreach (Dot tt in targetList)
            {

                int tDeltaX = (int)(tt.x - actor.x);
                int tDeltaY = (int)(tt.y - actor.y);
                int tDelta = (deltaX * deltaX) + (deltaY * deltaY);

                if (delta < tDelta)
                {
                    t = tt;
                }
            }

            return t;
        }

        public List<Dot> SetCourse(Dot actor, Dot target, bool away)
        {

            List<Dot> possibleCourse = new List<Dot>();

            int deltaX = (int)(target.x - actor.x);
            int deltaY = (int)(target.y - actor.y);
            int delta = (deltaX * deltaX) + (deltaY * deltaY);


            foreach (Dot neighbor in neighborEmptyList)
            {

                int magnetDeltaX = (int)(target.x - neighbor.x);
                int magentDeltaY = (int)(target.y - neighbor.y);
                int magnetDelta = (magnetDeltaX * magnetDeltaX) + (magentDeltaY * magentDeltaY);

                if (away == false)
                {
                    if (magnetDelta <= delta)
                    {
                        possibleCourse.Add(neighbor);
                    }
                }
                else if (away == true)
                {
                    if (magnetDelta > delta)
                    {
                        possibleCourse.Add(neighbor);
                    }
                }
            }

            return possibleCourse;
        }
        public void ReplicateEat(Dot actor, List<Dot> selection)
        {
            byte r = (byte)random.Next(selection.Count);
            UpdateDot(actor.playerID, actor.x, actor.y, 0, selection[r].x, selection[r].y);
            actorsToRetainList.Add(dots[actor.x][actor.y]);
            UpdateDot(actor.playerID, selection[r].x, selection[r].y, 0, actor.x, actor.y);
            actorsToRetainList.Add(dots[selection[r].x][selection[r].y]);
            eatFood++;
        }

        public void MoveAgainstTarget(Dot actor, List<Dot> selection, bool away)
        {
            Dot chosen = GetNearestDotFromFarTarget(actor, selection);
            List<Dot> courseList = SetCourse(actor, chosen, away);

            if (courseList.Count > 0)
            {
                byte r = (byte)random.Next(courseList.Count);
                UpdateDot(actor.playerID, courseList[r].x, courseList[r].y, 0, actor.x, actor.y);
                actorsToRetainList.Add(dots[courseList[r].x][courseList[r].y]);
                EraseDot(actor.x, actor.y);
            }
            else
            {
                MoveRoam(actor);
            }
        }


        //https://softologyblog.wordpress.com/2020/03/21/ant-colony-simulations/
        //Position is the X,Y coordinates of the ant. Direction is the angle the ant is facing.
        //Maximum angle determines how far an ant can turn left or right each step of the simulation. Move speed is how far the ant moves forward each simulation step.
        //If an ant hits the edge of the world it turns 90 degrees and keeps moving.
        public void MoveRoam(Dot actor)
        {
            byte r = (byte)random.Next(neighborEmptyList.Count);
            UpdateDot(actor.playerID, neighborEmptyList[r].x, neighborEmptyList[r].y, 0, actor.x, actor.y);
            actorsToRetainList.Add(dots[neighborEmptyList[r].x][neighborEmptyList[r].y]);
            EraseDot(actor.x, actor.y);

        }

        public void UpdateMap()
        {
            var pgSource = BitmapSource.Create(board, board, 96, 96, PixelFormats.Bgra32, null, imgdata, stride);
            pgSource.Freeze();
            CurrentPgImage = pgSource;
        }



        public void Calc(object? source, ElapsedEventArgs? e)
        {
            if (!Monitor.TryEnter(timerLock))
            {
                monitorHits++;
                MainWindow.main.monitorHits = monitorHits.ToString();
                return;
            }

            try
            {
                calcWatch.Start();

                foreach (Dot actor in actorsList)
                {
                    ScanNeighbors(actor);
                    bool ruleAccepted = false;

                    if (actor.playerID == 1)
                    ruleset = p1ruleset;
                    else if (actor.playerID == 2)
                    ruleset = p2ruleset;



                    for (int i = 0; i < ruleset.Count; i++)
                    {

                        if (ruleset[i].who == 0 && ruleset[i].where == 0)
                            referenceList = neighborFoodList;
                        else if (ruleset[i].who == 0 && ruleset[i].where == 1)
                            referenceList = farNeighborFoodList;
                        else if (ruleset[i].who == 0 && ruleset[i].where == 2)
                        {
                            concatReferenceList.Clear();
                            concatReferenceList.AddRange(farNeighborFoodList);
                            concatReferenceList.AddRange(neighborFoodList);
                            referenceList = concatReferenceList;
                        }
                        else if (ruleset[i].who == 1 && ruleset[i].where == 0)
                            referenceList = neighborAlliesList;
                        else if (ruleset[i].who == 1 && ruleset[i].where == 1)
                            referenceList = farNeighborAlliesList;
                        else if (ruleset[i].who == 1 && ruleset[i].where == 2)
                        {
                            concatReferenceList.Clear();
                            concatReferenceList.AddRange(farNeighborAlliesList);
                            concatReferenceList.AddRange(neighborAlliesList);
                            referenceList = concatReferenceList;
                        }
                        else if (ruleset[i].who == 2 && ruleset[i].where == 0)
                            referenceList = neighborEnemyList;
                        else if (ruleset[i].who == 2 && ruleset[i].where == 1)
                            referenceList = farNeighborEnemyList;
                        else if (ruleset[i].who == 2 && ruleset[i].where == 2)
                        {
                            concatReferenceList.Clear();
                            concatReferenceList.AddRange(farNeighborEnemyList);
                            concatReferenceList.AddRange(neighborEnemyList);
                            referenceList = concatReferenceList;
                        }
                        else if (ruleset[i].who == 3 && ruleset[i].where == 0)
                            referenceList = neighborEmptyList;
                        else if (ruleset[i].who == 3 && ruleset[i].where == 1)
                            referenceList = farNeighborEmptyList;
                        else if (ruleset[i].who == 3 && ruleset[i].where == 2)
                        {
                            concatReferenceList.Clear();
                            concatReferenceList.AddRange(farNeighborEmptyList);
                            concatReferenceList.AddRange(neighborEmptyList);
                        }

                        if (ruleset[i].and == 0)
                        {
                            RCompare = 0;
                            LCompare = 0;
                        }
                        else if (ruleset[i].and == 1 && ruleset[i].where == 0)
                        {
                            RCompare = neighborAlliesList.Count();
                            LCompare = neighborEnemyList.Count();
                        }
                        else if (ruleset[i].and == 1 && ruleset[i].where == 1)
                        {
                            RCompare = farNeighborAlliesList.Count();
                            LCompare = farNeighborEnemyList.Count();
                        }
                        else if (ruleset[i].and == 2 && ruleset[i].where == 0)
                        {
                            RCompare = neighborEnemyList.Count();
                            LCompare = neighborAlliesList.Count();
                        }
                        else if (ruleset[i].and == 2 && ruleset[i].where == 1)
                        {
                            RCompare = farNeighborEnemyList.Count();
                            LCompare = farNeighborAlliesList.Count();
                        }
                        else if (ruleset[i].and == 3 && ruleset[i].where == 0)
                        {
                            RCompare = neighborEmptyList.Count();
                            LCompare = neighborEnemyList.Count();
                        }
                        else if (ruleset[i].and == 3 && ruleset[i].where == 1)
                        {
                            RCompare = farNeighborEmptyList.Count();
                            LCompare = farNeighborEnemyList.Count();
                        }
                        else if (ruleset[i].and == 4 && ruleset[i].where == 0)
                        {
                            RCompare = neighborEmptyList.Count();
                            LCompare = neighborAlliesList.Count();
                        }
                        else if (ruleset[i].and == 4 && ruleset[i].where == 1)
                        {
                            RCompare = farNeighborEmptyList.Count();
                            LCompare = farNeighborAlliesList.Count();
                        }


                        if (ruleset[i].then == 0 && referenceList.Count != 0 && neighborEmptyList.Count != 0 && RCompare >= LCompare)
                        {
                            if (actor.playerID == 1)
                                p1ruleHit[i]++;
                            else if (actor.playerID == 2)
                                p2ruleHit[i]++;

                            MoveAgainstTarget(actor, referenceList, false);
                            ruleAccepted = true;
                            break;
                        }
                        else if (ruleset[i].then == 1 && referenceList.Count != 0 && neighborEmptyList.Count != 0 && RCompare >= LCompare)
                        {
                            if (actor.playerID == 1)
                                p1ruleHit[i]++;
                            else if (actor.playerID == 2)
                                p2ruleHit[i]++;
                            MoveAgainstTarget(actor, referenceList, true);
                            ruleAccepted = true;
                            break;
                        }

                        else if (ruleset[i].then == 2 && referenceList.Count != 0 && RCompare >= LCompare)
                        {
                            if (actor.playerID == 1)
                                p1ruleHit[i]++;
                            else if (actor.playerID == 2)
                                p2ruleHit[i]++;
                            ReplicateEat(actor, referenceList);
                            ruleAccepted = true;
                            break;
                        }

                        else if (ruleset[i].then == 3 && referenceList.Count != 0 && RCompare >= LCompare)
                        {
                            if (actor.playerID == 1)
                                p1ruleHit[i]++;
                            else if (actor.playerID == 2)
                                p2ruleHit[i]++;
                            MoveRoam(actor);
                            ruleAccepted = true;
                            break;
                        }
                        else if (ruleset[i].then == 4 && referenceList.Count != 0 && RCompare >= LCompare)
                        {
                            if (actor.playerID == 1)
                                p1ruleHit[i]++;
                            else if (actor.playerID == 2)
                                p2ruleHit[i]++;
                            actorsToRetainList.Add(dots[actor.x][actor.y]);
                            ruleAccepted = true;

                            break;
                        }
                    }

                    if (ruleAccepted == false)
                    {
                        RotateDot(actor.x, actor.y);
                        actorsToRetainList.Add(dots[actor.x][actor.y]);
                        rulesNoHit++;
                    }

                }


                actorsList.Clear();
                actorsList.AddRange(actorsToRetainList);
                actorsToRetainList.Clear();

                MainWindow.main.rulesNoHit = rulesNoHit.ToString();
                MainWindow.main.actors = actorsList.Count().ToString();
                MainWindow.main.eatenFood = eatFood.ToString();
                MainWindow.main.rotatedHits = rotatedHits.ToString();

                calcWatch.Stop();
                int calcFPS = Convert.ToInt32(1000 / calcWatch.Elapsed.TotalMilliseconds);
                MainWindow.main.calcFPS = calcFPS.ToString();
                calcWatch.Reset();

                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    graphicsWatch.Start();
                    UpdateMap();
                    graphicsWatch.Stop();
                    int graphicsFPS = Convert.ToInt32(1000 / graphicsWatch.Elapsed.TotalMilliseconds);
                    MainWindow.main.graphicsFPS = (graphicsFPS).ToString();
                    graphicsWatch.Reset();

                });




            }

            finally
            {
                Monitor.Exit(timerLock);
            }
        }



    }
    public partial class MainWindow : Window
    {
        internal static MainWindow main;
        internal static PgViewModel viewModel;
        internal string calcFPS
        {
            get { return calcFPScounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { calcFPScounter.Content = value; })); }
        }

        internal string graphicsFPS
        {
            get { return graphicsFPScounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { graphicsFPScounter.Content = value; })); }
        }

        internal string monitorHits
        {
            get { return monitorHitsCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { monitorHitsCounter.Content = value; })); }
        }

        internal string actors
        {
            get { return actorsCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { actorsCounter.Content = value; })); }
        }

        internal string eatenFood
        {
            get { return eatenFoodCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { eatenFoodCounter.Content = value; })); }
        }

        internal string rulesNoHit
        {
            get { return rulesNoHitCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { rulesNoHitCounter.Content = value; })); }
        }

        internal string rotatedHits
        {
            get { return rotateHits.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { rotateHits.Content = value; })); }
        }


        private void CreateRuleComboBox(int number, int playerID, int selectedWhoIndex, int selectedWhereIndex, int selectedAndIndex, int selectedThenIndex)
        {
            Thickness m;
            ComboBoxItem cboxitem;

            Label ifLabel = new Label();
            ifLabel.HorizontalAlignment = HorizontalAlignment.Left;
            ifLabel.VerticalAlignment = VerticalAlignment.Top;
            m = ifLabel.Margin;
            m.Top = gridTopOffset;
            ifLabel.Margin = m;
            ifLabel.Content = "Rule " + number + ": If";
            RuleGrid.Children.Add(ifLabel);

            ComboBox whoCbox = new ComboBox();
            whoCbox.HorizontalAlignment = HorizontalAlignment.Left;
            whoCbox.VerticalAlignment = VerticalAlignment.Top;
            m = whoCbox.Margin;
            m.Top = gridTopOffset;
            m.Left = 60;
            whoCbox.Margin = m;
            whoCbox.Width = 80;
            whoCbox.Height = 25;
            foreach (var c in workerWhoChoices)
            {
                cboxitem = new ComboBoxItem();
                cboxitem.Content = c.Item2;
                whoCbox.Items.Add(cboxitem);
            }
            whoCbox.SelectedIndex = selectedWhoIndex;
            RuleGrid.Children.Add(whoCbox);

            Label IsLabel = new Label();
            IsLabel.HorizontalAlignment = HorizontalAlignment.Left;
            IsLabel.VerticalAlignment = VerticalAlignment.Top;
            m = ifLabel.Margin;
            m.Top = gridTopOffset;
            m.Left = 140;
            IsLabel.Margin = m;
            IsLabel.Content = "is";
            RuleGrid.Children.Add(IsLabel);

            ComboBox whereCbox = new ComboBox();
            whereCbox.HorizontalAlignment = HorizontalAlignment.Left;
            whereCbox.VerticalAlignment = VerticalAlignment.Top;
            m = whereCbox.Margin;
            m.Top = gridTopOffset;
            m.Left = 160;
            whereCbox.Margin = m;
            whereCbox.Width = 70;
            whereCbox.Height = 25;
            foreach (var c in workerWhereChoices)
            {
                cboxitem = new ComboBoxItem();
                cboxitem.Content = c.Item2;
                whereCbox.Items.Add(cboxitem);
            }
            whereCbox.SelectedIndex = selectedWhereIndex;
            RuleGrid.Children.Add(whereCbox);


            Label andLabel = new Label();
            andLabel.HorizontalAlignment = HorizontalAlignment.Left;
            andLabel.VerticalAlignment = VerticalAlignment.Top;
            m = ifLabel.Margin;
            m.Top = gridTopOffset;
            m.Left = 230;
            andLabel.Margin = m;
            andLabel.Content = "and";
            RuleGrid.Children.Add(andLabel);


            ComboBox andCbox = new ComboBox();
            andCbox.HorizontalAlignment = HorizontalAlignment.Left;
            andCbox.VerticalAlignment = VerticalAlignment.Top;
            m = andCbox.Margin;
            m.Top = gridTopOffset;
            m.Left = 260;
            andCbox.Margin = m;
            andCbox.Width = 140;
            andCbox.Height = 25;
            foreach (var c in workerAndChoices)
            {
                cboxitem = new ComboBoxItem();
                cboxitem.Content = c.Item2;
                andCbox.Items.Add(cboxitem);
            }
            andCbox.SelectedIndex = selectedAndIndex;
            RuleGrid.Children.Add(andCbox);


            Label thenLabel = new Label();
            thenLabel.HorizontalAlignment = HorizontalAlignment.Left;
            thenLabel.VerticalAlignment = VerticalAlignment.Top;
            m = ifLabel.Margin;
            m.Top = gridTopOffset;
            m.Left = 400;
            thenLabel.Margin = m;
            thenLabel.Content = "then";
            RuleGrid.Children.Add(thenLabel);

            ComboBox thenCbox = new ComboBox();
            thenCbox.HorizontalAlignment = HorizontalAlignment.Left;
            thenCbox.VerticalAlignment = VerticalAlignment.Top;
            m = thenCbox.Margin;
            m.Top = gridTopOffset;
            m.Left = 440;
            thenCbox.Margin = m;
            thenCbox.Width = 90;
            thenCbox.Height = 25;
            foreach (var c in workerThenChoices)
            {
                cboxitem = new ComboBoxItem();
                cboxitem.Content = c.Item2;
                thenCbox.Items.Add(cboxitem);
            }
            thenCbox.SelectedIndex = selectedThenIndex;
            RuleGrid.Children.Add(thenCbox);

            Label hitsLabel = new Label();
            hitsLabel.Name = "hitsLabel";
            RegisterName("hitsLabel" + ruleNumber + playerID, hitsLabel);
            hitsLabel.HorizontalAlignment = HorizontalAlignment.Left;
            hitsLabel.VerticalAlignment = VerticalAlignment.Top;
            m = ifLabel.Margin;
            m.Top = gridTopOffset;
            m.Left = 530;
            hitsLabel.Margin = m;
            hitsLabel.Content = "0";
            RuleGrid.Children.Add(hitsLabel);

            if (playerID == 1)
            {
                p1rulesetHits.Add(hitsLabel);
                p1rulesetReference.Add((whoCbox, whereCbox, andCbox, thenCbox));
            }
            else if (playerID == 2)
            {
                p2rulesetHits.Add(hitsLabel);
                p2rulesetReference.Add((whoCbox, whereCbox, andCbox, thenCbox));

            }

            gridTopOffset = gridTopOffset + 40;

        }


        public MainWindow()
        {

            //https://stackoverflow.com/questions/8202844/extending-user-controls-in-wpf
            InitializeComponent();
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 1, 0, 0, 0, 2);
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 1, 0, 1, 0, 0);
            ruleNumber++;
             CreateRuleComboBox(ruleNumber, 1, 2, 0, 0, 1);
             ruleNumber++;
             CreateRuleComboBox(ruleNumber, 1, 2, 1, 1, 1);
             ruleNumber++;
             CreateRuleComboBox(ruleNumber, 1, 1, 1, 2, 1);
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 1, 3, 0, 0, 3);


            ruleNumber = 0;
            gridTopOffset = gridTopOffset + 80;

            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 2, 0, 0, 0, 2);
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 2, 0, 1, 0, 0);
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 2, 2, 0, 0, 1);
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 2, 2, 1, 1, 1);
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 2, 1, 1, 2, 1);
            ruleNumber++;
            CreateRuleComboBox(ruleNumber, 2, 3, 0, 0, 3);

            gTimer.Interval = TimeSpan.FromMilliseconds(100);
            gTimer.Tick += new EventHandler(updateCouters);

        }

        public void updateCouters(object sender, EventArgs e)
        {

            for (int i = 0; i < p1ruleHit.Length; i++)
            {
                p1rulesetHits[i].Content = (p1ruleHit[i]).ToString();
            }
            for (int i = 0; i < p2ruleHit.Length; i++)
            {
                p2rulesetHits[i].Content = (p2ruleHit[i]).ToString();
            }
        }
        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (aTimer.Enabled == false)
            {
                aTimer.Enabled = true;
                Thread.Sleep(1000);
            }
            else
            {
                aTimer.Enabled = false;
                Thread.Sleep(1000);
            }
        }


        private void Start_Click(object sender, RoutedEventArgs e)
        {

            aTimer = new System.Timers.Timer();
            main = this;
            viewModel = new PgViewModel();
            DataContext = viewModel;
            aTimer.Elapsed += new ElapsedEventHandler(viewModel.Calc);
            aTimer.Interval = Convert.ToInt32(1000 / targetFPS);

            targetFPSCounter.Content = targetFPS.ToString();

            scanDepth = Int32.Parse(ScanCtrl.SelectionBoxItem.ToString());


            if (gameStarted == false)
            {
                gameStarted = true;
                viewModel.InitPlaceSpawn();
                aTimer.Enabled = true;
                gTimer.Start();
            }
            else
            {
                aTimer.Enabled = false;
                Thread.Sleep(1000);

                imgdata = new byte[board * board * bytesperpixel];
                dots = InitDots();
                actorsList.Clear();
                actorsToRetainList.Clear();

                eatFood = 0;
                scanHits = 0;
                viewModel.InitPlaceSpawn();
                aTimer.Enabled = true;
            }

            p1ruleset.Clear();
            p2ruleset.Clear();
            foreach (var r in p1rulesetReference)
            {
                Rule rule = new Rule();
                rule.who = r.Item1.SelectedIndex;
                rule.where = r.Item2.SelectedIndex;
                rule.and = r.Item3.SelectedIndex;
                rule.then = r.Item4.SelectedIndex;
                p1ruleset.Add(rule);
            }
            foreach (var r in p2rulesetReference)
            {
                Rule rule = new Rule();
                rule.who = r.Item1.SelectedIndex;
                rule.where = r.Item2.SelectedIndex;
                rule.and = r.Item3.SelectedIndex;
                rule.then = r.Item4.SelectedIndex;
                p2ruleset.Add(rule);
            }
            p1ruleHit = new int[p1ruleset.Count];
            p2ruleHit = new int[p2ruleset.Count];
        }

        private void AddRule_Click(object sender, RoutedEventArgs e)
        {
            //ruleNumber++;
            //CreateRuleComboBox(ruleNumber, 3, 0, 3, 0);
        }

        private void ManualStart_Click(object sender, RoutedEventArgs e)
        {
            main = this;
            viewModel = new PgViewModel();
            DataContext = viewModel;

            scanDepth = Int32.Parse(ScanCtrl.SelectionBoxItem.ToString());


            if (gameStarted == false)
            {
                gameStarted = true;
                viewModel.InitPlaceSpawn();

            }
            else
            {
                Thread.Sleep(1000);

                imgdata = new byte[board * board * bytesperpixel];
                dots = InitDots();
                actorsList.Clear();
                actorsToRetainList.Clear();

                eatFood = 0;
                scanHits = 0;
                viewModel.InitPlaceSpawn();
            }

            p1ruleset.Clear();
            p2ruleset.Clear();
            foreach (var r in p1rulesetReference)
            {
                Rule rule = new Rule();
                rule.who = r.Item1.SelectedIndex;
                rule.where = r.Item2.SelectedIndex;
                rule.and = r.Item3.SelectedIndex;
                rule.then = r.Item4.SelectedIndex;
                p1ruleset.Add(rule);
            }
            foreach (var r in p2rulesetReference)
            {
                Rule rule = new Rule();
                rule.who = r.Item1.SelectedIndex;
                rule.where = r.Item2.SelectedIndex;
                rule.and = r.Item3.SelectedIndex;
                rule.then = r.Item4.SelectedIndex;
                p2ruleset.Add(rule);
            }
            p1ruleHit = new int[p1ruleset.Count];
            p2ruleHit = new int[p2ruleset.Count];
        }

        private void NextStep_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Calc(null, null);

            ComboBoxItem cboxitem;

            DotActorList.Items.Clear();
            var sortedActorList = actorsList.OrderBy(x => x.x);
            foreach (var c in sortedActorList)
            {
                cboxitem = new ComboBoxItem();
                cboxitem.Content = c.x + ":" + c.y;
                DotActorList.Items.Add(cboxitem);
            }
        }

        private void DotActorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            string[] s = cmb.SelectedItem.ToString().Split(":");
            short x = Convert.ToInt16(s[1]);
            short y = Convert.ToInt16(s[2]);

            viewModel.ScanNeighbors(dots[x][y]);
            viewModel.ColorDot(x, y, 0);

            if (NType.SelectedIndex == 0)
            {
                foreach (Dot v in actorsInSight)
                {
                    viewModel.ColorDot(v.x, v.y, 1);
                }
            }
            else if (NType.SelectedIndex == 1)
            {
                foreach (Dot v in neighborEmptyList)
                {
                    viewModel.ColorDot(v.x, v.y, 1);
                }
            }

            viewModel.UpdateMap();
            //DotInfo.Content = (x.ToString() + " " + y.ToString() + "\n" + n1 + v1);
        }




    }
}
