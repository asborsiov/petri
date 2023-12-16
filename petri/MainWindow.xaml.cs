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

namespace petri
{

    class PgViewModel : INotifyPropertyChanged
    {

        //Drawing board
        static object timerLock = new object();
        public static int board = 1200; //width and height of the game field
        public static int bytesperpixel = 4;
        public static int stride = board * bytesperpixel; 
        byte[] imgdata = new byte[board * board * bytesperpixel]; 

        //Other global variables

        //Jagged array is faster than 2d array or a single array for my data size
        //Did I properly init static array for a continious memory allocation?
        public static Dot[][] dots = InitDots();
        public static List<Dot> actorsList = new List<Dot>();
        [ThreadStatic]
        public static Random random = new Random();


        public struct Dot
        {
            public int playerID;
            public int targetDot;
            public int action;
            //I need x and y despite them being equal to the indexes because I don't know how to store a Dot[][] in actorsList and knowing their index at the same time. Is there any better way?
            public int x;
            public int y;
        }


        public static Dot[][] InitDots()
        {
            //Init jagged array, set borders and coordinates

            Dot[][] result = new Dot[board][];

            for (int x = 0; x < board; ++x)
            {
                result[x] = new Dot[board];
                for (int y = 0; y < board; ++y)
                {                 
                    result[x][y].x = x;
                    result[x][y].y = y;
                    if (x == 0 || x == board - 1 || y == 0 || y == board - 1)
                    {                                   
                    result[x][y].playerID = -1;
                    }
                    else
                    result[x][y].playerID = 0;

                }
            }              
            return result;
        }


        //Place random dots to start the game
        public void PlaceDot()
        {

            int randomPosX;
            int randomPosY;

            for (var i = 0; i < 5; i++)
            {
                randomPosX = random.Next(1, 1000);
                randomPosY = random.Next(1, 1000);

                if ((dots[randomPosX][randomPosY].playerID == 0))
                {
                    dots[randomPosX][randomPosY].playerID = 1;
                    dots[randomPosX][randomPosY].x = randomPosX;
                    dots[randomPosX][randomPosY].y = randomPosY;
                    actorsList.Add(dots[randomPosX][randomPosY]);

                }
            }
        }


        public void Calc(object source, ElapsedEventArgs e)
        {
            //Prevent multiple threads to add items in a single list
            if (!Monitor.TryEnter(timerLock))
            {
                return;
            }

             try
            {         

            //Copy the list, because we can't add new Dots while iterating them
            List <Dot> previousActorsList = new List<Dot>(actorsList);

            foreach (var actor in previousActorsList)
            {
                //List of possible directions to grow for this Dot
                List<Dot> decisionList = new List<Dot>();

                //Looking for 8 neighbors. A sector is a 3x3 box
                for (int sector_x = actor.x - 1; sector_x != actor.x + 2; sector_x++)
                {
                    for (int sector_y = actor.y - 1; sector_y != actor.y + 2; sector_y++)
                    {
                        if (dots[sector_x][sector_y].playerID == 0)
                        {
                            decisionList.Add(dots[sector_x][sector_y]);
                        }
                    }
                }
                
                //Procreate a new dot in random direction
                if (decisionList.Count != 0)
                {
                //remove this random and fix already existing static random
                Random rnd = new Random();
                
                int r = rnd.Next(decisionList.Count);
                Dot randomTarget = new Dot();
                randomTarget = decisionList[r];
                dots[randomTarget.x][randomTarget.y].playerID = 1;
                dots[randomTarget.x][randomTarget.y].x = randomTarget.x;
                dots[randomTarget.x][randomTarget.y].y = randomTarget.y;
                actorsList.Add(dots[randomTarget.x][randomTarget.y]);
                }
            }

            for (int row = 0; row < board; row++)
            {
                for (int col = 0; col < board; col++)
                {
                    if (dots[row][col].playerID == 1)
                    {
                        imgdata[row * stride  + col * 4 + 0] = Convert.ToByte((0.2) * 0xff);
                        imgdata[row * stride + col * 4 + 1] = Convert.ToByte((0.2) * 0xff);
                        imgdata[row * stride + col * 4 + 2] = Convert.ToByte((0.2) * 0xff);
                        imgdata[row * stride + col * 4 + 3] = Convert.ToByte((0.2) * 0xff);
                    }
                    else
                    {
                        imgdata[row * stride + col * 4 + 0] = Convert.ToByte(0xff);
                        imgdata[row * stride + col * 4 + 1] = Convert.ToByte(0xff);
                        imgdata[row * stride + col * 4 + 2] = Convert.ToByte(0xff);
                        imgdata[row * stride + col * 4 + 3] = Convert.ToByte(0xff);
                    }
                }
              }


            var gradient = BitmapSource.Create(board, board, 96, 96, PixelFormats.Bgra32, null, imgdata, stride);
            gradient.Freeze();

            CurrentPgImage = gradient;
            }
            finally
            {
                Monitor.Exit(timerLock);
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;
        private BitmapSource currentPgImage;
        public BitmapSource CurrentPgImage
        {
            get { return currentPgImage; }
            set
            {
                currentPgImage = value;
                PropertyChanged?.Invoke(
                    this, new PropertyChangedEventArgs("CurrentPgImage"));
            }
        }

    }

    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new PgViewModel();
            DataContext = viewModel;

            viewModel.PlaceDot();

            System.Timers.Timer aTimer = new System.Timers.Timer();
            aTimer.Elapsed += new ElapsedEventHandler(viewModel.Calc);
            aTimer.Interval = 1;
            aTimer.Enabled = true;
        }
    }
}