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

namespace petri
{

    class PgViewModel : INotifyPropertyChanged
    {

        //Drawing related
        static object timerLock = new object();
        public static int board = 500; //width and height of the game field
        public static int bytesperpixel = 4;
        public static int stride = board * bytesperpixel;
        public static byte[] imgdata = new byte[board * board * bytesperpixel];
        public event PropertyChangedEventHandler PropertyChanged;
        public static Stopwatch calcWatch = new Stopwatch();
        public static Stopwatch graphicsWatch = new Stopwatch();
        public static int monitorHits = 0;
        public static int scanHits = 0;
        public static int targetFPS = 60;
        public static int lowestFPScalc = 1000000;
        public static int lowestFPSgraph = 1000000;
        public static BitmapSource currentPgImage = new WriteableBitmap(board, board, 96, 96, PixelFormats.Bgr32, null);
        public BitmapSource CurrentPgImage
        {
            get { return currentPgImage; }
            set
            {
                currentPgImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPgImage"));
            }
        }

        public static Dot[][] dots = InitDots();
        public static List<Dot> actorsList = new List<Dot>();
        public static List<Dot> actorsToRetainList = new List<Dot>();
        public static int eatenFood = 0;

        public Random random = new Random();

        //Make a dict with Point as key?
        public struct Dot
        {
            public byte playerID;
           // public byte density;
            public byte type; //0 - worker, 1 - warrior, 10 - resource, 11 empty
            public ushort x;
            public ushort y;
        }


        public static Dot[][] InitDots()
        {
            //Init jagged array, set borders and coordinates

            Dot[][] result = new Dot[board][];

            for (ushort x = 0; x < board; ++x)
            {
                result[x] = new Dot[board];
                for (ushort y = 0; y < board; ++y)
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



        //Place random dots to start the game
        public void InitPlaceSpawn()
        {

            ushort randomPosX;
            ushort randomPosY;
            for (var i = 0; i < 20000; i++)
            {
                randomPosX = (ushort)random.Next(50, 450);
                randomPosY = (ushort)random.Next(50, 450);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDot(1, randomPosX, randomPosY, 0);
                    actorsList.Add(dots[randomPosX][randomPosY]);
                }
            }

            for (var i = 0; i < 20000; i++)
            {
                randomPosX = (ushort)random.Next(50, 450);
                randomPosY = (ushort)random.Next(50, 450);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDot(2, randomPosX, randomPosY, 0);
                    actorsList.Add(dots[randomPosX][randomPosY]);
                }
            }
            for (var i = 0; i < 1; i++)
            {
                randomPosX = (ushort)random.Next(30, 450);
                randomPosY = (ushort)random.Next(30, 450);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    UpdateDot(100, randomPosX, randomPosY, 100);
                }
            }
        }

        public void UpdateDot(byte playerID, ushort x, ushort y, byte type)
        {
            byte color;
            dots[x][y].type = type;
            dots[x][y].playerID = playerID;
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

        public void EraseDot(ushort x, ushort y)
        {
            dots[x][y].type = 0;
            dots[x][y].playerID = 0;
            imgdata[x * stride + y * 4 + 0] = 0;
            imgdata[x * stride + y * 4 + 1] = 0;
            imgdata[x * stride + y * 4 + 2] = 0;
            imgdata[x * stride + y * 4 + 3] = 0;
            
        }



        public (List<Dot>, List<Dot>, List<Dot>, List<Dot>, List<Dot>, List<Dot>, List<Dot>, List<Dot>) ScanNeighbors(Dot actor)
        {
            //Moving this lists to static rises FPS from 6 to 9

            List<Dot> neighborFoodList = new List<Dot>();
            List<Dot> neighborEmptyList = new List<Dot>();
            List<Dot> neighborFriendsList = new List<Dot>();
            List<Dot> neighborEnemyList = new List<Dot>();

            for (int sector_x = actor.x - 1; sector_x != actor.x + 2; sector_x++)
            {
                for (int sector_y = actor.y - 1; sector_y != actor.y + 2; sector_y++)
                {
                    if (sector_x == actor.x && sector_y == actor.y)
                    {
                        continue;
                    }
                    else if (dots[sector_x][sector_y].playerID == 255)
                    {
                        continue;
                    }
                    else if (dots[sector_x][sector_y].playerID == 100)
                    {
                        neighborFoodList.Add(dots[sector_x][sector_y]);
                    }
                    else if (dots[sector_x][sector_y].playerID == 0)
                    {
                        neighborEmptyList.Add(dots[sector_x][sector_y]);
                    }
                    else if (dots[sector_x][sector_y].playerID == actor.playerID)
                    {
                        neighborFriendsList.Add(dots[sector_x][sector_y]);
                    }
                    else if (dots[sector_x][sector_y].playerID != actor.playerID)
                    {
                        neighborEnemyList.Add(dots[sector_x][sector_y]);
                    }
                }
            }

            List<Dot> farNeighborFoodList = new List<Dot>();
            List<Dot> farNeighborEmptyList = new List<Dot>();
            List<Dot> farNeighborFriendsList = new List<Dot>();
            List<Dot> farNeighborEnemyList = new List<Dot>();

            for (int sector_x = actor.x - 3; sector_x != actor.x + 4; sector_x++)
            {
                for (int sector_y = actor.y - 3; sector_y != actor.y + 4; sector_y++)
                {
                    if (sector_x == actor.x && sector_y == actor.y)
                    {
                        continue;
                    }
                    if (sector_x == actor.x - 1 || sector_y == actor.y - 1 || sector_x == actor.x + 1 || sector_y == actor.y + 1)
                    {
                        continue;
                    }
                    else if (dots[sector_x][sector_y].playerID == 100)
                    {
                        farNeighborFoodList.Add(dots[sector_x][sector_y]);
                    }
                    else if (dots[sector_x][sector_y].playerID == 0)
                    {
                        farNeighborEmptyList.Add(dots[sector_x][sector_y]);
                    }
                    else if (dots[sector_x][sector_y].playerID == actor.playerID)
                    {
                        farNeighborFriendsList.Add(dots[sector_x][sector_y]);
                    }
                    else if (dots[sector_x][sector_y].playerID != actor.playerID)
                    {
                        farNeighborEnemyList.Add(dots[sector_x][sector_y]);
                    }
                }
            }

            return (neighborFoodList, neighborEmptyList, neighborFriendsList, neighborEnemyList, farNeighborFoodList, farNeighborEmptyList, farNeighborFriendsList, farNeighborEnemyList);
        }


        public List<Dot> GetCourse(Dot actor, List<Dot> targetList, List<Dot> neighborList)
        {
            //List<Dot> buffer = new List<Dot>();
            //List<Dot> magnetList = new List<Dot>();
            //Dot randomMagnet = new Dot();

            List<Dot> possibleCourse = new List<Dot>();
            byte rt = (byte)random.Next(targetList.Count);


            foreach (Dot neighbor in neighborList)
            {

            
            int deltaX = (int)(targetList[rt].x - actor.x);
            int deltaY = (int)(targetList[rt].y - actor.y);

            int magnetDeltaX = (int)(targetList[rt].x - neighbor.x);
            int magentDeltaY = (int)(targetList[rt].y - neighbor.y);

            int delta = (int)((deltaX * deltaX) + (deltaY * deltaY));
            int magnetDelta = (int)((magnetDeltaX * magnetDeltaX) + (magentDeltaY * magentDeltaY));


            if (magnetDelta < delta)
            {
                    possibleCourse.Add(neighbor);
            }

        }
            return possibleCourse;
        }

        public void Calc(object source, ElapsedEventArgs e)
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
                List<Dot> targetList = new List<Dot>();
                Dot target = new Dot();
                target.x = 300;
                target.y = 300;
                targetList.Add(target);
                foreach (Dot actor in actorsList)
                {

                    //List<Dot> neighborList = ScanNeighbors(a);

                   (List<Dot> neighborFoodList, List<Dot> neighborEmptyList, List<Dot> neighborFriendsList, List<Dot> neighborEnemyList, List<Dot> farNeighborFoodList, List<Dot> farNeighborEmptyList, List<Dot> farNeighborFriendsList, List<Dot> farNeighborEnemyList) = ScanNeighbors(actor);

                    if (neighborFoodList.Count != 0 && neighborEmptyList.Count != 0)
                    {
                        byte r = (byte)random.Next(neighborFoodList.Count);
                        UpdateDot(actor.playerID, actor.x, actor.y, actor.type);
                        actorsToRetainList.Add(dots[actor.x][actor.y]);
                        UpdateDot(actor.playerID, neighborFoodList[r].x, neighborFoodList[r].y, 0);
                        actorsToRetainList.Add(dots[neighborFoodList[r].x][neighborFoodList[r].y]);
                        eatenFood++;
                    }
                    //else if (neighborEmptyList.Count != 1 && neighborFoodList.Count != 0 && rnd < 5)
                    //{
                    //    byte r = (byte)random.Next(neighborEmptyList.Count);
                    //    UpdateDot(actor.playerID, neighborEmptyList[r].x, neighborEmptyList[r].y, 0);
                    //    actorsToRetainList.Add(dots[neighborEmptyList[r].x][neighborEmptyList[r].y]);
                    //    EraseDot(actor.x, actor.y);
                    //}
                    //else if (neighborFriendsList.Count != 0 && neighborEnemyList.Count != 0 && neighborFriendsList.Count > neighborEnemyList.Count)
                    //{
                    //    byte r = (byte)random.Next(neighborEnemyList.Count);
                    //    UpdateDot(0, neighborEnemyList[r].x, neighborEnemyList[r].y, 0);
                    //    actorsToRetainList.Add(dots[actor.x][actor.y]);
                    //}
                    //else if (neighborFriendsList.Count != 0 && neighborEnemyList.Count != 0 && neighborFriendsList.Count < neighborEnemyList.Count)
                    //{
                    //    //This should not exist, there must be no self-harm actions. In previous rule, the dot must be marked dead and skipped the calcucations
                    //    UpdateDot(0, actor.x, actor.y, 0);
                    //}
                    else if (neighborEmptyList.Count != 0 && neighborFoodList.Count == 0)
                    {

                        //we must know where dot concentration bigger (possibly by quardic spearation) and remove random from getcourse, possibly turn a list to a dot
                        //when cell moves, it LOOKS at quadrant and can't change it's position then?
                        List<Dot> courseList = GetCourse(actor, targetList, neighborEmptyList);
                        if (courseList.Count > 0)
                        {
                            byte r = (byte)random.Next(courseList.Count);
                            UpdateDot(actor.playerID, courseList[r].x, courseList[r].y, 0);
                            actorsToRetainList.Add(dots[courseList[r].x][courseList[r].y]);
                            EraseDot(actor.x, actor.y);
                        }
                        else
                        {
                            byte r = (byte)random.Next(neighborEmptyList.Count);
                            UpdateDot(actor.playerID, neighborEmptyList[r].x, neighborEmptyList[r].y, 0);
                            actorsToRetainList.Add(dots[neighborEmptyList[r].x][neighborEmptyList[r].y]);
                            EraseDot(actor.x, actor.y);
                        }

                    }
                    //else if (neighborEmptyList.Count != 0 && neighborFoodList.Count == 0)
                    //{
                    //    byte r = (byte)random.Next(neighborEmptyList.Count);
                    //    UpdateDot(actor.playerID, neighborEmptyList[r].x, neighborEmptyList[r].y, 0);
                    //    actorsToRetainList.Add(dots[neighborEmptyList[r].x][neighborEmptyList[r].y]);
                    //    UpdateDot(0, actor.x, actor.y, 0);
                    //}
                    else
                    {
                        //  keep immobilized dots alive for awhile
                        UpdateDot(actor.playerID, actor.x, actor.y, actor.type);
                        actorsToRetainList.Add(dots[actor.x][actor.y]);
                    }
                    
                }

                actorsList.Clear();
                actorsList.AddRange(actorsToRetainList);
                actorsToRetainList.Clear();

                MainWindow.main.actors = actorsList.Count().ToString();
                MainWindow.main.eatenFood = eatenFood.ToString();

                calcWatch.Stop();
                int calcFPS = Convert.ToInt32(1000 / calcWatch.Elapsed.TotalMilliseconds);
                MainWindow.main.calcFPS = calcFPS.ToString();
                calcWatch.Reset();

                App.Current.Dispatcher.Invoke((Action)delegate
                {
                    graphicsWatch.Start();


                    var pgSource = BitmapSource.Create(board, board, 96, 96, PixelFormats.Bgra32, null, imgdata, stride);
                    pgSource.Freeze();
                    CurrentPgImage = pgSource;

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

        internal string scans
        {
            get { return scannedTimesCounter.Content.ToString(); }
            set { Dispatcher.Invoke(new Action(() => { scannedTimesCounter.Content = value; })); }
        }


        public MainWindow()
        {
            InitializeComponent();
        
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            main = this;
            var viewModel = new PgViewModel();
            DataContext = viewModel;

            viewModel.InitPlaceSpawn();
            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(viewModel.Calc);
            aTimer.Interval = Convert.ToInt32(1000 / targetFPS);
            aTimer.Enabled = true;
            targetFPSCounter.Content = targetFPS.ToString();
        }
    }
}

