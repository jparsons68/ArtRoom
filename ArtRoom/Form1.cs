using ChartLib;
using IERSInterface;
using QSFileReader;
using QSGeometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ArtRoom
{
    public partial class Form1 : Form
    {
        DoubleBuffer dbuffer;
        TooltipContainer ttC0, ttC1, ttcA;
        DragAndDropHelper ddH;

        //      PointD _tr_aux0;
        PointD _tr_m0, _tr_m1, _tr_m2;
        PointD _tr_distancePoint;
        PointD _tr_measuringPoint0;
        PointD _tr_measuringPoint1;
        PointD _tr_sidePoint1;

        double _tr_screenPicHeightAtM0;

        PointD _tr_tL;
        PointD _tr_top, _tr_base, _tr_bL, _tr_bR, _tr_tR;
        PointD bL, bR, tL, tR;
        PointD dbL, dbR, dtL, dtR;
        PointD _tr_thickBL, _tr_thickBR, _tr_thickTL, _tr_thickTR;
        Matrix matrixR, matrixRinv, matrixT, matrixTinv;

        double zoomF = 1.0;

        AR_Settings settings = new AR_Settings();

        string appLocalPath;
        string lastFile;

        YLScsDrawing.Imaging.Filters.FreeTransform filter = new YLScsDrawing.Imaging.Filters.FreeTransform();

        public Form1()
        {
            InitializeComponent();

            appLocalPath = ApplicationNiceties.ApplicationSetup.CreateApplicationResourcesPath();
            //bm = new Bitmap(@"C:\Users\James\Documents\Visual Studio 2017\Projects\_JP_Hobby Apps\ArtRoom\ArtRoom\TestImages\FullSizeRender.jpg");
            // scrolledDoubleBuffer1.DataWidth = bm.Width;
            // scrolledDoubleBuffer1.DataHeight = bm.Height;
            scrolledDoubleBuffer1.DataWidth = 1000;
            scrolledDoubleBuffer1.DataHeight = 1000;

            dbuffer = scrolledDoubleBuffer1.DoubleBuffer;
            dbuffer.PaintEvent += DoubleBuffer_PaintEvent;
            dbuffer.MouseClick += Dbuffer_MouseClick;
            dbuffer.MouseDown += Dbuffer_MouseDown;
            dbuffer.MouseMove += Dbuffer_MouseMove;
            dbuffer.MouseUp += Dbuffer_MouseUp;

            ddH = new DragAndDropHelper(dbuffer);
            ddH.FileAction = dropFileAction;
            ddH.Extensions = new string[] { ".*" };

            ttC0 = new TooltipContainer(dbuffer);
            ttC1 = new TooltipContainer(dbuffer);
            ttcA = new TooltipContainer(dbuffer);

            // read settings
            lastFile = System.IO.Path.Combine(appLocalPath, "lastSettings");
            // default settings
            if (!readSettings(lastFile)) defaultSettings();

            _uiUpdate();
            _geometryUpdate();
            scrolledDoubleBuffer1.Invalidate();
        }

        private void defaultSettings()
        {
            pictureItem = null;
            filter.Bitmap = null;
            settings.Default();
        }

        private bool readSettings(string path)
        {
            AR_Settings read = XML_IO.Read(path, typeof(AR_Settings)) as AR_Settings;
            if (read == null) return (false);
            settings = read;


            if (settings.BmMain != null)
            {
                scrolledDoubleBuffer1.DataWidth = settings.BmMain.Width;
                scrolledDoubleBuffer1.DataHeight = settings.BmMain.Height;
            }
            else
            {
                scrolledDoubleBuffer1.DataWidth = 1000;
                scrolledDoubleBuffer1.DataHeight = 1000;
            }
            filter.Bitmap = settings.BmPic;
            settings.Path = path;
            return (true);
        }


        private void dropFileAction(Object sender, string path)
        {
            var pt = dbuffer.PointToClient(MousePosition);
            var data = ttcA.GetDataAtPointer(pt);
            PointD ptD = data as PointD;
            if (ptD != null && ptD == settings.PicCorner)
            {
                settings.PictureImagePath = path;
                filter.Bitmap = settings.BmPic;
                filter.IsBilinearInterpolation = true;
                _geometryUpdate();
                dbuffer.Refresh();
            }
            else
            {
                settings.MainImagePath = path;

                if (settings.BmMain != null)
                {
                    scrolledDoubleBuffer1.DataWidth = settings.BmMain.Width;
                    scrolledDoubleBuffer1.DataHeight = settings.BmMain.Height;
                }
                else
                {
                    scrolledDoubleBuffer1.DataWidth = 1000;
                    scrolledDoubleBuffer1.DataHeight = 1000;
                }

                dbuffer.Invalidate();
            }

        }



        private void tstb_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            { _geometryUpdate(); dbuffer.Refresh(); }
        }

        private void tsbSHOWTRANS_CheckStateChanged(object sender, EventArgs e)
        {
            dbuffer.Refresh();
        }

        void _uiUpdate()
        {
            tstbMEASURE.Text = settings.MeasureDim.ToString();
            tstbWIDTH.Text = settings.PicWidth.ToString();
            tstbHEIGHT.Text = settings.PicHeight.ToString();
            tstbDEPTH.Text = settings.PicDepth.ToString();
        }
        void _geometryUpdate()
        {
            _parseSizes();
            _tr_top = null;
            _tr_base = null;
            pictureItem = null;
            // calc distance vp->
            Vector3 hv1;
            var box0 = settings.Box0;
            var box1 = settings.Box1;
            if (box0.VanishingPoint != null && box1.VanishingPoint != null)
            {
                hv1 = new Vector3(box1.VanishingPoint.X - box0.VanishingPoint.X, box1.VanishingPoint.Y - box0.VanishingPoint.Y, 0.0);
                hv1.Normalize();
                var cos = hv1.X;
                var sin = -hv1.Y;
                matrixR = new Matrix((float)cos, (float)-sin, (float)sin, (float)cos, 0f, 0f);
                matrixR.Invert();
                matrixRinv = new Matrix((float)cos, (float)-sin, (float)sin, (float)cos, 0f, 0f);

                matrixT = new Matrix();
                matrixT.Translate((float)-box0.VanishingPoint.X, (float)-box0.VanishingPoint.Y);

                matrixTinv = new Matrix();
                matrixTinv.Translate((float)box0.VanishingPoint.X, (float)box0.VanishingPoint.Y);



                _transformBox(box0);
                _transformBox(box1);


                _tr_measuringPoint0 = null;
                _tr_measuringPoint1 = null;

                _tr_m0 = null;
                _tr_m1 = null;
                _tr_m2 = null;

                //      _tr_aux0 = (settings.Aux0 != null) ? transformPoint(settings.Aux0) : null;
                _tr_distancePoint = null;

                var m0 = settings.M0;
                var m1 = settings.M1;
                if (m0 != null)
                {
                    _tr_m0 = transformPoint(m0);
                    double x1 = box1.TransVanishingPoint.X - _tr_m0.X;
                    double x0 = _tr_m0.X - box0.TransVanishingPoint.X;
                    double s = Math.Sqrt(x1 * x0);
                    if (_tr_m0.Y < box0.TransVanishingPoint.Y) s *= -1;
                    _tr_distancePoint = new PointD(_tr_m0.X, (box0.TransVanishingPoint.Y + s));

                    double d0 = QSGeometry.QSGeometry.Distance(_tr_distancePoint, box0.TransVanishingPoint);
                    double d1 = QSGeometry.QSGeometry.Distance(_tr_distancePoint, box1.TransVanishingPoint);

                    _tr_measuringPoint0 = new PointD(box0.TransVanishingPoint.X + hv1.X * d0, box0.TransVanishingPoint.Y);
                    _tr_measuringPoint1 = new PointD(box1.TransVanishingPoint.X - hv1.X * d1, box1.TransVanishingPoint.Y);
                }

                settings.M2 = null;
                if (m0 != null && m1 != null)
                {
                    double u;
                    var ptOnLine = QSGeometry.QSGeometry.ClosestPointOnLineExtended(box1.VanishingPoint.X, box1.VanishingPoint.Y, m0.X, m0.Y, m1.X, m1.Y, out u);
                    settings.M2 = new PointD(ptOnLine.X, ptOnLine.Y);
                    //settings.M1 = new PointD(ptOnLine.X, ptOnLine.Y);
                    _tr_m1 = transformPoint(settings.M1);
                    _tr_m2 = transformPoint(settings.M2);

                }



                double x, y;
                _tr_sidePoint1 = null;
                if (_tr_measuringPoint1 != null && _tr_m2 != null &&
                    QSGeometry.QSGeometry.SegmentIntersect(_tr_measuringPoint1.X, _tr_measuringPoint1.Y, _tr_m2.X, _tr_m2.Y, _tr_m0.X, _tr_m0.Y, _tr_m0.X + 100, _tr_m0.Y, false, out x, out y))
                {
                    _tr_sidePoint1 = new PointD(x, y);
                }




                // if we have sizes..
                // vertical part
                _tr_screenPicHeightAtM0 = double.NaN;
                if (_tr_sidePoint1 != null)
                {
                    double screenDim = Math.Abs(_tr_sidePoint1.X - _tr_m0.X);
                    _tr_screenPicHeightAtM0 = screenDim * settings.PicHeight / settings.MeasureDim;
                }

                _tr_tL = _tr_bL = _tr_tR = _tr_bR = null;
                dbL = dbR = dtL = dtR = null;
                bL = bR = tL = tR = null;

                _tr_thickBL = _tr_thickBR = _tr_thickTL = _tr_thickTR = null;
                if (settings.PicCorner != null)
                {
                    _tr_tL = transformPoint(settings.PicCorner);
                    PointD leftVert = new PointD(_tr_tL.X, _tr_tL.Y + 100);
                    if (_tr_m0 != null)
                    {
                        // intersect piccorner to vp1
                        // with vertical thru _tr_m0
                        PointD _tr_m0b = new PointD(_tr_m0.X, _tr_m0.Y + 100);
                        _tr_top = _intersect(_tr_m0, _tr_m0b, _tr_tL, box1.TransVanishingPoint);
                        if (!double.IsNaN(_tr_screenPicHeightAtM0)) _tr_base = new PointD(_tr_top.X, _tr_top.Y + _tr_screenPicHeightAtM0);
                        if (_tr_base != null)
                        {
                            _tr_bL = _intertsect(_tr_base, box1.TransVanishingPoint, _tr_tL, leftVert);

                            double heightAtLeft = _tr_bL.Y - _tr_tL.Y;
                            double widthFromBL = heightAtLeft * settings.PicWidth / settings.PicHeight;
                            var sideR = new PointD(_tr_bL.X + widthFromBL, _tr_bL.Y);
                            _tr_bR = _intersect(sideR, _tr_measuringPoint1, _tr_bL, box1.TransVanishingPoint);
                            var tmp = new PointD(_tr_bR.X, _tr_bR.Y - 100);
                            _tr_tR = _intersect(_tr_tL, box1.TransVanishingPoint, _tr_bR, tmp);


                            double depthFromBL = heightAtLeft * settings.PicDepth / settings.PicHeight;
                            var tmpD = new PointD(_tr_bL.X + depthFromBL, _tr_bL.Y);
                            _tr_thickBL = _intersect(tmpD, _tr_measuringPoint0, box0.TransVanishingPoint, _tr_bL);
                            tmpD = new PointD(_tr_tL.X + depthFromBL, _tr_tL.Y);
                            _tr_thickTL = _intersect(tmpD, _tr_measuringPoint0, box0.TransVanishingPoint, _tr_tL);

                            double heightAtRight = _tr_bR.Y - _tr_tR.Y;
                            double depthFromBR = heightAtRight * settings.PicDepth / settings.PicHeight;
                            var tmpC = new PointD(_tr_bR.X + depthFromBR, _tr_bR.Y);
                            _tr_thickBR = _intersect(tmpC, _tr_measuringPoint0, box0.TransVanishingPoint, _tr_bR);
                            tmpC = new PointD(_tr_tR.X + depthFromBR, _tr_tR.Y);
                            _tr_thickTR = _intersect(tmpC, _tr_measuringPoint0, box0.TransVanishingPoint, _tr_tR);

                        }
                    }


                    bL = inverseTransformPoint(_tr_bL);
                    bR = inverseTransformPoint(_tr_bR);
                    tL = inverseTransformPoint(_tr_tL);
                    tR = inverseTransformPoint(_tr_tR);

                    dbL = inverseTransformPoint(_tr_thickBL);
                    dbR = inverseTransformPoint(_tr_thickBR);
                    dtL = inverseTransformPoint(_tr_thickTL);
                    dtR = inverseTransformPoint(_tr_thickTR);

                    try
                    {
                        vertex[0] = new PointF((float)dtL.X, (float)dtL.Y);
                        vertex[1] = new PointF((float)dtR.X, (float)dtR.Y);
                        vertex[2] = new PointF((float)dbR.X, (float)dbR.Y);
                        vertex[3] = new PointF((float)dbL.X, (float)dbL.Y);
                        filter.FourCorners = vertex;
                        pictureItem = filter.Bitmap;
                        imageLocation = filter.ImageLocation;
                    }
                    catch
                    {
                        pictureItem = null;
                    }
                }
            }




        }



        //Bitmap shadow(Bitmap srce)
        //{
        //    Bitmap outBM = new Bitmap(srce);

        //    for (int x = 0; x < outBM.Width; x++)
        //    {
        //        for (int y = 0; y < outBM.Height; y++)
        //        {
        //            try
        //            {
        //                var pix = outBM.GetPixel(x, y);
        //                pix.R = pix.G = pix.B = 0;
        //                outBM.SetPixel(x, y, pix);
        //                Color prevX = newBitmap.GetPixel(x - blurAmount, y);
        //                Color nextX = newBitmap.GetPixel(x + blurAmount, y);
        //                Color prevY = newBitmap.GetPixel(x, y - blurAmount);
        //                Color nextY = newBitmap.GetPixel(x, y + blurAmount);

        //                int avgR = (int)((prevX.R + nextX.R + prevY.R + nextY.R) / 4);
        //                int avgG = (int)((prevX.G + nextX.G + prevY.G + nextY.G) / 4);
        //                int avgB = (int)((prevX.B + nextX.B + prevY.B + nextY.B) / 4);

        //                newBitmap.SetPixel(x, y, Color.FromArgb(avgR, avgG, avgB));
        //            }
        //            catch (Exception) { }
        //        }
        //    }









        //   }

        Bitmap pictureItem;
        Point imageLocation = new Point();
        PointF[] vertex = new PointF[4];

        private PointD _intersect(PointD a, PointD b, PointD c, PointD d)
        {
            double x, y;
            if (QSGeometry.QSGeometry.SegmentIntersect(a.X, a.Y, b.X, b.Y, c.X, c.Y, d.X, d.Y, false, out x, out y))
            {
                return (new PointD(x, y));
            }
            return (null);
        }

        private void _transformBox(AR_PersectiveBox box)
        {
            List<PointD> tmp = new List<PointD>();
            for (int i = 0; i < 4; i++)
            {
                var pt = box.Points[i];
                tmp.Add(transformPoint(pt));
            }
            box.TransPoints = tmp.ToArray();

            if (box.VanishingPoint != null)
            {
                var trvp = new PointD(box.VanishingPoint.X, box.VanishingPoint.Y);
                box.TransVanishingPoint = transformPoint(trvp);
            }
            else box.TransVanishingPoint = null;

        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (settings.Path == null) { saveAsToolStripMenuItem_Click(sender, e); return; }
            XML_IO.Write(settings.Path, settings);
            XML_IO.Write(lastFile, settings);
        }

        string _recentSettings = "RecentSettings";
        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = IERSInterface.FileFieldAndBrowser.PopupSaveFileDialog("Save", this, _recentSettings, "XML|*.xml",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (path == null) return;
            settings.Path = path;
            XML_IO.Write(path, settings);
            XML_IO.Write(lastFile, settings);

        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string path = IERSInterface.FileFieldAndBrowser.PopupOpenFileDialog("Open", this, _recentSettings, "XML|*.xml",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (path == null) return;
            if (!readSettings(path)) defaultSettings();
            _uiUpdate();
            _geometryUpdate();
            scrolledDoubleBuffer1.Invalidate(true);
        }
        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            defaultSettings();
            _uiUpdate();
            _geometryUpdate();
            scrolledDoubleBuffer1.Invalidate(true);
        }
        private PointD transformPoint(PointD pt)
        {
            PointF[] tmp = new PointF[] { new PointF((float)pt.X, (float)pt.Y) };
            matrixT.TransformPoints(tmp);
            matrixR.TransformPoints(tmp);
            matrixTinv.TransformPoints(tmp);
            return (new PointD(tmp[0].X, tmp[0].Y));
        }

        private PointD inverseTransformPoint(PointD pt)
        {
            if (pt == null) return (null);
            PointF[] tmp = new PointF[] { new PointF((float)pt.X, (float)pt.Y) };
            matrixT.TransformPoints(tmp);
            matrixRinv.TransformPoints(tmp);
            matrixTinv.TransformPoints(tmp);
            return (new PointD(tmp[0].X, tmp[0].Y));
        }




        PointF transformPoint(PointF point)
        {
            PointF[] tmp = new PointF[] { point };
            matrixT.TransformPoints(tmp);
            matrixR.TransformPoints(tmp);
            matrixTinv.TransformPoints(tmp);
            return (tmp[0]);
        }
        PointF[] transformPoints(PointF[] points)
        {
            PointF[] tmp = points;
            matrixT.TransformPoints(tmp);
            matrixR.TransformPoints(tmp);
            matrixTinv.TransformPoints(tmp);
            return (tmp);
        }


        AR_PersectiveBox pbActive = null;
        PointD auxActive;

        private void printToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Bitmap pBm = new Bitmap(settings.BmMain);
            Graphics g = Graphics.FromImage(pBm);
            normalMode = false;
            DoubleBuffer_PaintEvent(dbuffer, new PaintEventArgs(g, new Rectangle(0, 0, pBm.Width, pBm.Height)));
            string tStr = DateTime.Now.ToString("AR_hhmm_ss_ddMMyyyy");
            pBm.Save(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), tStr+".jpg"));
            normalMode = true;
        }

        bool normalMode = true;
        PointF _data2screen(PointD dataPt)
        {
            if (normalMode)
                return (new PointF((float)scrolledDoubleBuffer1.Data2screenX(dataPt.X), (float)scrolledDoubleBuffer1.Data2screenY(dataPt.Y)));

            // printing
            return (new PointF((float)dataPt.X, (float)dataPt.Y));
        }
        PointF _data2screen(int x, int y)
        {
            if (normalMode)
                return (new PointF((float)scrolledDoubleBuffer1.Data2screenX(x), (float)scrolledDoubleBuffer1.Data2screenY(y)));

            // printing
            return (new PointF((float)x, (float)y));
        }

        int activeIdx = -1;

        private void toolStripButton1_CheckStateChanged(object sender, EventArgs e)
        {
            //shadow
            dbuffer.Refresh();
        }

        bool moveWholePic = false;
        bool moveShadow = false;
        int startX, startY, mX, mY;

        private void decreaseShadowBorderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.ShadowBorder--;
            if (settings.ShadowBorder < 0) settings.ShadowBorder = 0;
            dbuffer.Refresh();
        }

        private void increaseOpacityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.ShadowOpacity += 10;
            if (settings.ShadowOpacity > 255) settings.ShadowOpacity = 255;
            dbuffer.Refresh();
        }

        private void decreaseOpacityToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.ShadowOpacity -= 10;
            if (settings.ShadowOpacity < 0) settings.ShadowOpacity = 0;
            dbuffer.Refresh();
    }

        private void increaseShadowBorderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.ShadowBorder++;
            dbuffer.Refresh();
        }

        private void deceaseBlurToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.BlurAmount--;
            if (settings.BlurAmount< 0) settings.BlurAmount= 0;
            dbuffer.Refresh();
        }

        private void increaseBlurToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settings.BlurAmount++;
            dbuffer.Refresh();
        }

        PointF startPt;
        private void Dbuffer_MouseDown(object sender, MouseEventArgs e)
        {
            fineControl = false;
            moveWholePic = false;
            moveShadow = false;
            startX = e.X;
            startY = e.Y;
            fcX = mX = vX = e.X;
            fcY = mY = vY = e.Y;


            if (ChartLib.ChartViewer.IsModifierHeld(Keys.Control))
            {
                moveShadow = true;
                toolStripButtonSHADOW.Checked = true;
                startShadowX = settings.ShadowOffset.X;
                startShadowY = settings.ShadowOffset.Y;
                return;
            }
            //{
            //    settings.Aux0 = new PointD(x, y);
            //    _geometryUpdate();
            //    dbuffer.Refresh();
            //}
            //else 


            if (settings.PicCorner != null)
                startPt = _data2screen(settings.PicCorner);
            object data;
            data = ttC0.GetDataAtPointer(e);
            if (data != null) { pbActive = settings.Box0; activeIdx = (int)data; return; }
            data = ttC1.GetDataAtPointer(e);
            if (data != null) { pbActive = settings.Box1; activeIdx = (int)data; return; }
            data = ttcA.GetDataAtPointer(e);
            if (data != null)
            {
                auxActive = data as PointD;
                moveWholePic = (settings.PicCorner == auxActive);
                return;
            }

            // nothing else, initialite the pic
            if (settings.PicCorner == null)
            {
                double x = scrolledDoubleBuffer1.Screen2dataX(e.X);
                double y = scrolledDoubleBuffer1.Screen2dataY(e.Y);
                settings.PicCorner = new PointD(x, y);
                startPt = _data2screen(settings.PicCorner);
                moveWholePic = true;
            }
        }

        bool fineControl = false;
        int fcX, fcY, vX, vY;
        private double startShadowX;
        private double startShadowY;

        private void Dbuffer_MouseMove(object sender, MouseEventArgs e)
        {
            bool fineControlNow = ChartViewer.IsModifierHeld(Keys.Shift);
            if (fineControlNow != fineControl) { mX = e.X; mY = e.Y; fcX = vX; fcY = vY; }
            fineControl = fineControlNow;
            if (fineControl) { vX = fcX + (e.X - mX) / 5; vY = fcY + (e.Y - mY) / 5; }
            else { vX = fcX + (e.X - mX); vY = fcY + (e.Y - mY); }
            double x = scrolledDoubleBuffer1.Screen2dataX(vX);
            double y = scrolledDoubleBuffer1.Screen2dataY(vY);




            if (moveShadow)
            {
                int dX = vX - startX;
                int dY = vY - startY;
                settings.ShadowOffset.X = startShadowX + dX / zoomF;
                settings.ShadowOffset.Y = startShadowY + dY / zoomF;
                dbuffer.Refresh();
            }
            else if (moveWholePic)
            {
                int dX = vX - startX;
                int dY = vY - startY;
                x = scrolledDoubleBuffer1.Screen2dataX((int)startPt.X + dX);
                y = scrolledDoubleBuffer1.Screen2dataY((int)startPt.Y + dY);
                settings.PicCorner = new PointD(x, y);
                _geometryUpdate();
                dbuffer.Refresh();
            }
            else if (pbActive != null)
            {
                pbActive.Points[activeIdx].X = x;
                pbActive.Points[activeIdx].Y = y;
                pbActive.Refresh();
                _geometryUpdate();
                dbuffer.Refresh();
            }
            else if (auxActive != null)
            {
                auxActive.X = x;
                auxActive.Y = y;
                _geometryUpdate();
                dbuffer.Refresh();
            }

        }

        private void Dbuffer_MouseUp(object sender, MouseEventArgs e)
        {
            pbActive = null;
            activeIdx = -1;
            auxActive = null;
            moveWholePic = false;
            moveShadow = false;

            if (settings.M2 != null)
                settings.M1 = new PointD(settings.M2.X, settings.M2.Y);
            _geometryUpdate();
            dbuffer.Refresh();
        }




        void _parseSizes()
        {
            settings.MeasureDim = _tryParse(tstbMEASURE);
            settings.PicWidth = _tryParse(tstbWIDTH);
            settings.PicHeight = _tryParse(tstbHEIGHT);
            settings.PicDepth = _tryParse(tstbDEPTH);
        }

        private double _tryParse(ToolStripTextBox tstb)
        {
            try
            {
                double d = double.Parse(tstb.Text);
                return (d);
            }
            catch (Exception)
            {
                return (double.NaN);
            }
        }

        private void Dbuffer_MouseClick(object sender, MouseEventArgs e)
        {
            double x = scrolledDoubleBuffer1.Screen2dataX(e.X);
            double y = scrolledDoubleBuffer1.Screen2dataY(e.Y);
            //if (ChartLib.ChartViewer.IsModifierHeld(Keys.Control))
            //{
            //    settings.Aux0 = new PointD(x, y);
            //    _geometryUpdate();
            //    dbuffer.Refresh();
            //}
            //else 

            if (ChartLib.ChartViewer.IsModifierHeld(Keys.Shift))
            {
                if (settings.M0 == null)
                    settings.M0 = new PointD(x, y);
                else if (settings.M1 == null)
                    settings.M1 = new PointD(x, y);
                _geometryUpdate();
                dbuffer.Refresh();
            }
        }
        private void DoubleBuffer_PaintEvent(object sender, PaintEventArgs e)
        {
            toolStripLabelINFO.Text = "Blur:" + settings.BlurAmount + " Opa:" + settings.ShadowOpacity + " Bdr:" + settings.ShadowBorder;
            zoomF = 1.0;

            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            ttC0.Clear();
            ttC1.Clear();

            Brush br = new SolidBrush(Color.FromArgb(150, Color.Black));
            Pen oPen = new Pen(br, 4f);

            RectangleF imageRect = RectangleF.Empty;
            if (settings.BmMain != null)
            {
                var bm = settings.BmMain;
                var pt0 = _data2screen(0, 0);
                var pt1 = _data2screen(bm.Width, 0);
                var pt2 = _data2screen(bm.Width, bm.Height);
                var pt3 = _data2screen(0, bm.Height);
                imageRect = new RectangleF(pt0.X, pt0.Y, pt1.X - pt0.X, pt2.Y - pt1.Y);
                e.Graphics.DrawImage(bm, imageRect);
                zoomF = ((double)imageRect.Width) / settings.BmMain.Width;
            }

            // make a region
            if (dtL != null && dtR != null && dbL != null && dbR != null)
            {
                // SHADOW
                if (toolStripButtonSHADOW.Checked && !imageRect.IsEmpty)
                {
                    // make a bitmap same area
                    Bitmap shadowBM = new Bitmap(settings.BmMain.Width, settings.BmMain.Height);
                    var sGraphics = Graphics.FromImage(shadowBM);
                    sGraphics.SmoothingMode = SmoothingMode.HighQuality;
                    sGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    // clear, transparent
                    sGraphics.Clear(Color.Transparent);
                    Brush sBr = new SolidBrush(Color.FromArgb(settings.ShadowOpacity, Color.Black));


                    // get region of back
                    List<PointF> poly = new List<PointF>();
                    poly.Add(new PointF((float)tL.X, (float)tL.Y));
                    poly.Add(new PointF((float)tR.X, (float)tR.Y));
                    poly.Add(new PointF((float)bR.X, (float)bR.Y));
                    poly.Add(new PointF((float)bL.X, (float)bL.Y));

                    Region reg = ChartLib.RegionPathHelper.GetRegion(poly);
                    Region uReg = new Region();
                    uReg.MakeEmpty();
                    // translate and union bigger area
                    uReg.Union(reg);
                    reg.Translate((float)settings.ShadowOffset.X, (float)settings.ShadowOffset.Y);                    uReg.Union(reg);
                    reg.Translate(2*settings.ShadowBorder, 0f); uReg.Union(reg);
                    reg.Translate(0, 2*settings.ShadowBorder); uReg.Union(reg);
                    reg.Translate(-2 * settings.ShadowBorder, 0f); uReg.Union(reg);
                    // fill the region in translucent black
                    sGraphics.FillRegion(sBr, uReg);

                    // blur it
                    StackBlur.StackBlur.Process(shadowBM, settings.BlurAmount);
                    // draw
                    e.Graphics.DrawImage(shadowBM, imageRect);

                }


                _fill(e.Graphics, Brushes.DimGray, bL, dbL, dtL, tL);
                _fill(e.Graphics, Brushes.DimGray, bL, bR, dbR, dbL);
                _fill(e.Graphics, Brushes.DimGray, tR, dtR, dbR, bR);
                _fill(e.Graphics, Brushes.DimGray, tL, tR, dtR, dtL);

                PointF[] clipRegPt = new PointF[4];
                clipRegPt[0] = _data2screen(dtL);
                clipRegPt[1] = _data2screen(dtR);
                clipRegPt[2] = _data2screen(dbR);
                clipRegPt[3] = _data2screen(dbL);
                Region clipRegion = RegionPathHelper.GetRegion(clipRegPt);
                ttcA.Add(clipRegion, null, settings.PicCorner);
                e.Graphics.FillPolygon(Brushes.LightGray, clipRegPt);

                if (pictureItem != null)
                {
                    e.Graphics.FillPolygon(Brushes.DimGray, clipRegPt);
                    e.Graphics.SetClip(clipRegion, CombineMode.Replace);
                    // draw image.. expand slightly to fix bad edges
                    var pt0 = _data2screen(imageLocation.X, imageLocation.Y);
                    var pt1 = _data2screen(imageLocation.X + pictureItem.Width, imageLocation.Y);
                    var pt2 = _data2screen(imageLocation.X + pictureItem.Width, imageLocation.Y + pictureItem.Height);
                    var pt3 = _data2screen(imageLocation.X, imageLocation.Y + pictureItem.Height);
                    RectangleF rect = new RectangleF(pt0.X, pt0.Y, pt1.X - pt0.X, pt2.Y - pt1.Y);
                    rect.Inflate(3, 3);
                    e.Graphics.DrawImage(pictureItem, rect);
                    e.Graphics.ResetClip();
                    br = new SolidBrush(Color.FromArgb(20, Color.Black));
                    oPen = new Pen(br, 2f);
                    e.Graphics.DrawPolygon(oPen, clipRegPt);
                }
            }
            var box0 = settings.Box0;
            var box1 = settings.Box1;

            if (tsbSHOWTRANS.Checked)
            {
                // trans space
                _drawTransBox(e.Graphics, box0);
                _drawTransBox(e.Graphics, box1);

                _drawPoint(e.Graphics, _tr_m0, Brushes.YellowGreen);
                _drawPoint(e.Graphics, _tr_m2, Brushes.YellowGreen);
                //      _drawPoint(e.Graphics, _tr_aux0, Brushes.White);

                // line - measuring point thru m2 to y level of m0, the
                _drawLine(e.Graphics, Pens.Wheat, _tr_sidePoint1, _tr_measuringPoint1);
                _drawLine(e.Graphics, Pens.Wheat, _tr_sidePoint1, _tr_m0);

                _drawLine(e.Graphics, Pens.Gold, box0.TransVanishingPoint, box1.TransVanishingPoint);

                _drawLine(e.Graphics, Pens.LightYellow, box0.TransVanishingPoint, _tr_distancePoint);
                _drawLine(e.Graphics, Pens.LightYellow, box1.TransVanishingPoint, _tr_distancePoint);

                _drawPoint(e.Graphics, _tr_measuringPoint0, Brushes.Goldenrod);
                _drawPoint(e.Graphics, _tr_measuringPoint1, Brushes.Goldenrod);

                _drawPoint(e.Graphics, _tr_tL, Brushes.White);
                _drawPoint(e.Graphics, _tr_bL, Brushes.White);
                _drawPoint(e.Graphics, _tr_bR, Brushes.White);
                _drawPoint(e.Graphics, _tr_tR, Brushes.White);

                _drawLine(e.Graphics, Pens.White, box1.TransVanishingPoint, _tr_tL);

                _drawPoint(e.Graphics, _tr_top, Brushes.LightGoldenrodYellow);
                _drawPoint(e.Graphics, _tr_base, Brushes.LightGoldenrodYellow);

                //_drawLine(e.Graphics, Pens.Cyan, _tr_sideL, _tr_measuringPoint0);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickBL, _tr_thickBR);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickTL, _tr_thickBL);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickTL, _tr_thickTR);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickBR, _tr_thickTR);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickBL, _tr_bL);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickTL, _tr_tL);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickTR, _tr_tR);
                _drawLine(e.Graphics, Pens.Yellow, _tr_thickBR, _tr_bR);
            }





            if (tsbPERSP.Checked)
            {
                _drawLine(e.Graphics, Pens.OrangeRed, bL, bR);
                _drawLine(e.Graphics, Pens.OrangeRed, bR, tR);
                _drawLine(e.Graphics, Pens.OrangeRed, tR, tL);
                _drawLine(e.Graphics, Pens.OrangeRed, tL, bL);
                _drawLine(e.Graphics, Pens.Orange, dbL, bL);
                _drawLine(e.Graphics, Pens.Orange, dbR, bR);
                _drawLine(e.Graphics, Pens.Orange, dtR, tR);
                _drawLine(e.Graphics, Pens.Orange, dtL, tL);
                _drawPoint(e.Graphics, bL, Brushes.Orange);
                _drawPoint(e.Graphics, bR, Brushes.Orange);
                _drawPoint(e.Graphics, tL, Brushes.Orange);
                _drawPoint(e.Graphics, tR, Brushes.Orange);

            }


            if (tsbPERSP.Checked)
            {
                _drawBox(e.Graphics, box0, ttC0);
                _drawBox(e.Graphics, box1, ttC1);

                _drawLine(e.Graphics, Pens.RoyalBlue, box0.VanishingPoint, box1.VanishingPoint);

                //_drawLine(e.Graphics, Pens.Blue, box0.VanishingPoint, settings.Aux0);
                // _drawLine(e.Graphics, Pens.Blue, box1.VanishingPoint, settings.Aux0);
                //    _drawPoint(e.Graphics, settings.Aux0, Brushes.Green, ttcA);
            }


            if (tsbMEASURE.Checked)
            {
                _drawLine(e.Graphics, Pens.Black, settings.M0, settings.M2);
                _drawPoint(e.Graphics, settings.M0, Brushes.Black, ttcA);
                _drawPoint(e.Graphics, settings.M1, Brushes.Transparent, ttcA);
                _drawPoint(e.Graphics, settings.M2, Brushes.Black);

                if (settings.M0 != null && settings.M2 != null)
                {
                    var pt0 = _data2screen(settings.M0);
                    var pt1 = _data2screen(settings.M2);
                    e.Graphics.DrawString(settings.MeasureDim + "m", this.Font, Brushes.Yellow, (pt0.X + pt1.X) / 2, (pt0.Y + pt1.Y) / 2);
                }

            }

            if (tsbPERSP.Checked)
            {
                _drawLine(e.Graphics, Pens.Orange, dbL, dbR);
                _drawLine(e.Graphics, Pens.Orange, dbR, dtR);
                _drawLine(e.Graphics, Pens.Orange, dtR, dtL);
                _drawLine(e.Graphics, Pens.Orange, dtL, dbL);
            }

            e.Graphics.DrawString(settings.PicWidth + " x " + settings.PicHeight + "m", new Font(this.Font.FontFamily, 20f, FontStyle.Bold, GraphicsUnit.Pixel), Brushes.Black, -1, 0);
            e.Graphics.DrawString(settings.PicWidth + " x " + settings.PicHeight + "m", new Font(this.Font.FontFamily, 20f, FontStyle.Bold, GraphicsUnit.Pixel), Brushes.Black, 1, 0);
            e.Graphics.DrawString(settings.PicWidth + " x " + settings.PicHeight + "m", new Font(this.Font.FontFamily, 20f, FontStyle.Bold, GraphicsUnit.Pixel), Brushes.Black, 0, 1);
            e.Graphics.DrawString(settings.PicWidth + " x " + settings.PicHeight + "m", new Font(this.Font.FontFamily, 20f, FontStyle.Bold, GraphicsUnit.Pixel), Brushes.Black, 0, -1);
            e.Graphics.DrawString(settings.PicWidth + " x " + settings.PicHeight + "m", new Font(this.Font.FontFamily, 20f, FontStyle.Bold, GraphicsUnit.Pixel), Brushes.Yellow, 0, 0);
        }

        private List<PointF> _fill(Graphics graphics, Brush br, PointD a, PointD b, PointD c, PointD d)
        {
            if (a == null || b == null || c == null | d == null) return (null);
            List<PointF> spt = new List<PointF>();
            spt.Add(_data2screen(a));
            spt.Add(_data2screen(b));
            spt.Add(_data2screen(c));
            spt.Add(_data2screen(d));
            graphics.FillPolygon(br, spt.ToArray());
            return (spt);
        }
        private List<PointF> _fill(Graphics graphics, Brush br, Pen oPen, PointD a, PointD b, PointD c, PointD d)
        {
            if (a == null || b == null || c == null | d == null) return (null);
            List<PointF> spt = new List<PointF>();
            spt.Add(_data2screen(a));
            spt.Add(_data2screen(b));
            spt.Add(_data2screen(c));
            spt.Add(_data2screen(d));
            graphics.FillPolygon(br, spt.ToArray());
            if (oPen != null) graphics.DrawPolygon(oPen, spt.ToArray());
            return (spt);
        }

        Point _pointToScreenF(PointD pt)
        {
            return (scrolledDoubleBuffer1.Data2screen(pt.X, pt.Y));
        }

        private void _drawPoint(Graphics g, PointD pt, Brush br)
        {
            if (pt == null) return;
            var vp0 = _data2screen(pt);
            RectangleF rectA = new RectangleF(vp0.X - 2, vp0.Y - 2, 5f, 5f);
            g.FillRectangle(br, rectA);
        }
        private void _drawPoint(Graphics g, PointD pt, Brush br, TooltipContainer ttc)
        {
            if (pt == null) return;
            var vp0 = _data2screen(pt);
            RectangleF rectA = new RectangleF(vp0.X - 2, vp0.Y - 2, 5f, 5f);
            g.FillRectangle(br, rectA);
            rectA.Inflate(3, 3);
            ttc.Add(rectA, null, pt);
        }

        private void _drawLine(Graphics g, Pen pen, PointD pt0, PointD pt1)
        {
            if (pt0 == null) return;
            if (pt1 == null) return;
            var vp0 = _data2screen(pt0);
            var vp1 = _data2screen(pt1);
            g.DrawLine(pen, vp0, vp1);
        }
        private void _drawTransBox(Graphics graphics, AR_PersectiveBox box)
        {
            if (box.TransPoints == null) return;
            List<PointF> drawPt = new List<PointF>();
            foreach (var item in box.TransPoints)
                drawPt.Add(_data2screen(item));
            graphics.DrawPolygon(Pens.Yellow, drawPt.ToArray());
        }

        private void _drawBox(Graphics graphics, AR_PersectiveBox box, TooltipContainer ttC)
        {
            List<PointF> drawPt = new List<PointF>();
            foreach (var item in box.Points)
                drawPt.Add(_data2screen(item));
            Pen pen = new Pen(Color.SkyBlue); pen.DashStyle = DashStyle.Dot;

            graphics.DrawLine(Pens.LightBlue, drawPt[0], drawPt[1]);
            graphics.DrawLine(Pens.LightBlue, drawPt[2], drawPt[3]);
            graphics.DrawLine(pen, drawPt[1], drawPt[2]);
            graphics.DrawLine(pen, drawPt[0], drawPt[3]);
            if (box.VanishingPoint != null)
            {
                var vp = _data2screen(box.VanishingPoint);
                graphics.DrawLine(pen, vp, drawPt[0]);
                graphics.DrawLine(pen, vp, drawPt[1]);
                graphics.DrawLine(pen, vp, drawPt[2]);
                graphics.DrawLine(pen, vp, drawPt[3]);
            }

            int idx = -1;
            foreach (var item in drawPt)
            {
                idx++;
                RectangleF rect = new RectangleF(item.X - 2, item.Y - 2, 5f, 5f);
                graphics.DrawRectangle(Pens.DodgerBlue, rect);
                rect.Inflate(10, 10);
                ttC.Add(rect, null, idx);
            }
        }
    }

    public class AR_PersectiveBox
    {
        public PointD[] Points { get; set; } = new PointD[4];
        public PointD VanishingPoint { get; set; }

        [XmlIgnore]
        public PointD[] TransPoints { get; set; }
        [XmlIgnore]
        public PointD TransVanishingPoint { get; set; }

        public AR_PersectiveBox()
        {

        }
        public AR_PersectiveBox(float x, float y0, float y1, float w, float h0, float h1)
        {
            Points[0] = new PointD(x, y0);
            Points[1] = new PointD(x + w, y1);
            Points[2] = new PointD(x + w, y1 + h1);
            Points[3] = new PointD(x, y0 + h0);
            Refresh();
        }

        public void Refresh()
        {
            bool state = QSGeometry.QSGeometry.SegmentIntersect(Points[0].X, Points[0].Y, Points[1].X, Points[1].Y,
                 Points[3].X, Points[3].Y, Points[2].X, Points[2].Y,
                 false, out double x, out double y);
            VanishingPoint = state ? new PointD(x, y) : null;
        }

    }


    public class AR_Settings
    {
        public AR_Settings()
        {
            Default();
        }

        internal void Default()
        {
            MeasureDim = 1.0;
            PicWidth = 3.0;
            PicHeight = 1.2;
            PicDepth = 0.05;
            float w = 1000 / 5;
            float h0 = 1000 / 3;
            float h1 = 1000 / 4;
            float y0 = h0;
            float y1 = (h0 - h1) / 2 + y0;
            Box0 = new AR_PersectiveBox(w, y1, y0, w, h1, h0);
            Box1 = new AR_PersectiveBox(w * 3, y0, y1, w, h0, h1);
            MainImagePath = null;
            PictureImagePath = null;
            Path = null;
            M0 = null;
            M1 = null;
            M2 = null;
            PicCorner = null;
            Aux0 = null;
            ShadowOffset = new PointD(-10, 10);
            BlurAmount = 10;
            ShadowBorder = 5;
            ShadowOpacity = 100;
        }

        string mainPath, picPath;
        Bitmap bmPic, bmMain;
        public string MainImagePath
        {
            get { return (mainPath); }
            set
            {
                try
                {
                    mainPath = value;
                    bmMain = mainPath == null ? null : new Bitmap(mainPath);
                }
                catch
                {
                    bmMain = null;
                }
            }
        }

        public string PictureImagePath
        {
            get { return (picPath); }
            set
            {
                try
                {
                    picPath = value;
                    bmPic = picPath == null ? null : new Bitmap(picPath);
                }
                catch
                {
                    bmPic = null;
                }
            }
        }


        public Bitmap BmPic { get { return (bmPic); } }
        public Bitmap BmMain { get { return (bmMain); } }

        public PointD Aux0 { get; set; }

        public PointD ShadowOffset { get; set; }
        public float ShadowBorder { get; set; }
        public int BlurAmount { get; set; }
        public int ShadowOpacity { get; set; }

        public PointD M0 { get; set; }
        public PointD M1 { get; set; }
        public PointD M2 { get; set; }
        public PointD PicCorner { get; set; }
        public double MeasureDim { get; set; }
        public double PicWidth { get; set; }
        public double PicHeight { get; set; }
        public double PicDepth { get; set; }
        public AR_PersectiveBox Box0 { get; set; }
        public AR_PersectiveBox Box1 { get; set; }
        public string Path { get; set; }
    }
}
