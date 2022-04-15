using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DynamicDraw
{
    public partial class CurveGraph : UserControl
    {
        #region Members
        /// <summary>
        /// The distance for the mouse to move before considering a click a drag event.
        /// </summary>
        private static readonly int MOUSE_MOVE_THRESHOLD = 4;

        /// <summary>
        /// The visual radius of a control point.
        /// </summary>
        private static readonly int CONTROL_POINT_RADIUS = 5;

        /// <summary>
        /// The radius used to determine if a control point is clicked.
        /// </summary>
        private static readonly int CONTROL_POINT_CLICK_RADIUS = 10;

        /// <summary>
        /// Identifies how the curve graph can be used.
        /// </summary>
        public enum CurveEditMode
        {
            /// <summary>
            /// The curve can only be viewed and is non-interactive.
            /// </summary>
            ViewOnly,

            /// <summary>
            /// Existing points on the curve can be repositioned, but addition and deletion is not allowed.
            /// </summary>
            RepositionEdit,

            /// <summary>
            /// Points can be repositioned, deleted, or added.
            /// </summary>
            FullEdit
        }

        /// <summary>
        /// Tracks the initial point where the user clicks within the curve graph.
        /// </summary>
        private Point? mouseDownLoc;

        /// <summary>
        /// Tracks the current mouse location at any time, if editing is enabled.
        /// </summary>
        private Point mouseLoc;

        /// <summary>
        /// Tracks the active control point index in curveControlPoints if in use. -1 indicates no point selected.
        /// </summary>
        private int selectedControlPoint;

        /// <summary>
        /// These control points are used to produce a curve which modifies a linear domain, e.g. pressure sensitivity,
        /// which goes from 0 to x depending on a tablet's pressure resolution. The modified curve allows the domain
        /// to be treated as linear, exponential, logarithmic, etc. depending on the curve chosen. These points are
        /// values from (0,0) to (1,1) which define that curve. The curve drawn between them is a cubic Hermite
        /// cardinal spline according to the implementation of DrawCurves(). Note a line from 0,0 to 1,1 will match the
        /// domain. If fewer than 2 points exist, a perfect line is assumed.
        /// </summary>
        private PointF[] curveControlPoints;

        /// <summary>
        /// The number of points in the curve table. Having more points takes more memory, but makes look-ups faster,
        /// especially for discrete domains where the number of possible values matches the resolution (meaning, no
        /// interpolation). Default value is 1000.
        /// </summary>
        public int CurveTableResolution { get; set; }

        /// <summary>
        /// Any time the control points of the curve are modified, a table of values is generated correlating the
        /// domain to the curve, which reduces the computation of x(t) to random access or a lerp at worst. The number
        /// of points depends on the curve table resolution and are evenly spaced from 0 to the domain max.
        /// </summary>
        public float[] CurveTable { get; private set; }

        /// <summary>
        /// The way in which the curve graph can be used. Equal to <see cref="CurveEditMode.FullEdit"/> by default.
        /// </summary>
        public CurveEditMode EditMode;
        #endregion

        #region Constructors
        public CurveGraph(CurveEditMode editMode, int tableResolution) : this()
        {
            EditMode = editMode;
            CurveTable = new float[tableResolution];
            CurveTableResolution = tableResolution;
        }

        public CurveGraph()
        {
            InitializeComponent();
            DoubleBuffered = true;

            EditMode = CurveEditMode.FullEdit;
            curveControlPoints = new PointF[] { PointF.Empty, new PointF(1, 1) };
            CurveTableResolution = 100;
            CurveTable = new float[100];
            mouseLoc = Point.Empty;
            mouseDownLoc = null;
            selectedControlPoint = -1;

            canvas.Paint += Canvas_Paint;
            canvas.MouseDown += Canvas_MouseDown;
            canvas.MouseUp += Canvas_MouseUp;
            canvas.MouseMove += Canvas_MouseMove;
            PreviewKeyDown += CurveGraph_PreviewKeyDown;

            // Double buffering exists and works on panel but is not exposed by API. This avoids subclassing instead.
            // See https://stackoverflow.com/a/31562892
            typeof(Panel).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, canvas, new object[] { true });
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Sorts the control points and updates the curve table. 
        /// </summary>
        public void SetCurvePoints(IEnumerable<PointF> points)
        {
            curveControlPoints = points.OrderBy((point) => point.X).ToArray();
            CurveTable = GetCurveTable(curveControlPoints, CurveTableResolution);
            Refresh();
        }

        /// <summary>
        /// Regenerates the curve table as necessary.
        /// </summary>
        public static float[] GetCurveTable(PointF[] curveControlPoints, int curveTableResolution)
        {
            // Don't construct a table for fewer than 2 control points.
            if (curveControlPoints.Length < 2)
            {
                return Array.Empty<float>();
            }

            // Don't construct a table for a perfectly linear graph.
            if (curveControlPoints.Length == 2 &&
                curveControlPoints[0].Equals(PointF.Empty)
                && curveControlPoints[1].X == 1 && curveControlPoints[1].Y == 1)
            {
                return Array.Empty<float>();
            }

            // Approximates a number of points along the path
            using (var path = new GraphicsPath())
            {
                PointF[] points = curveControlPoints;
                List<PointF> pointList = curveControlPoints.ToList();

                path.AddCurve(points);

                using (var unitMatrix = new Matrix(curveTableResolution, 0, 0, curveTableResolution, 0, 0))
                {
                    // Creates a dense amount of coords along the curve that aren't evenly spread.
                    path.Flatten(unitMatrix, 0.0005f);
                }

                var newCurveTable = new Dictionary<float, float>();
                PointF[] pathPoints = path.PathPoints;

                // Having points fall along whole numbers makes random access comparisons easy by casting to int or
                // doing a quick lerp between two randomly-accessed values.
                float[] curveTable = new float[curveTableResolution];
                curveTable[0] = 0;
                curveTable[curveTableResolution - 1] = curveControlPoints[curveControlPoints.Length - 1].X == 1
                    ? pathPoints[pathPoints.Length - 1].Y
                    : 0;

                int prevX = 1;
                int prevIndex = 0;
                float highestX = 0;
                float tempAvg = 0;

                // The path points are a non-uniform dense approximation of the curve. Their x values are not whole
                // numbers, but they should be in order to achieve random access or to quickly lerp between two points.
                // This iterates through them (assumed to be in order by X) and averages all the Y values that fall
                // near each whole number from 0 to the curve table resolution.
                for (int i = 1; i < pathPoints.Length - 1; i++)
                {
                    // Ignore backwards-moving data that arises when control points are very close.
                    // Ignore the first bucket from -0.5 to +0.5, especially since data from -0.5 to 0 isn't collected.
                    if (pathPoints[i].X <= highestX || pathPoints[i].X < 0.5f)
                    {
                        prevIndex++;
                        continue;
                    }

                    highestX = pathPoints[i].X;

                    // Buckets are half offset because averaging points from -0.5 to +0.5 is more accurate than 0-1
                    if (pathPoints[i].X > prevX + 0.5f)
                    {
                        // It's common a path point will skip an entire bucket (or multiple). In that case, fill the
                        // array for those indices with values of -1 to indicate an unfilled bucket. After the averages
                        // are computed, it becomes trivial to loop back through and give lerped values to these.
                        int numberOfBucketsSkipped = (int)(pathPoints[i].X - prevX - 1);

                        for (int j = 0; j < numberOfBucketsSkipped; j++)
                        {
                            curveTable[prevX + j] = -1; // indicates an unfilled bucket
                        }
                        prevX += numberOfBucketsSkipped;

                        // Ignore the last bucket from resolution - 0.5f to resolution +0.5f especially since data
                        // beyond the resolution isn't collected.
                        if (prevX == curveTableResolution - 1)
                        {
                            break;
                        }

                        // Averages the values into the bucket.
                        int numberOfPoints = i - prevIndex;
                        for (; prevIndex < i; prevIndex++)
                        {
                            tempAvg += pathPoints[prevIndex].Y;
                        }

                        curveTable[prevX] = tempAvg / numberOfPoints;
                        if (curveTable[prevX] < 0) { curveTable[prevX] = 0; }
                        if (curveTable[prevX] > curveTableResolution) { curveTable[prevX] = curveTableResolution; }
                        if (pathPoints[i].X > curveTableResolution) { break; }
                        tempAvg = 0;
                        prevX++;
                    }
                }

                // Iterate through the array and assign lerped values to any unfilled areas.
                int firstUnfilledBucket = -1;
                for (int i = 0; i < curveTable.Length; i++)
                {
                    if (curveTable[i] == -1 && firstUnfilledBucket == -1)
                    {
                        firstUnfilledBucket = i;
                    }
                    else if (firstUnfilledBucket != -1 && curveTable[i] != -1)
                    {
                        float startingValue = firstUnfilledBucket != 0
                            ? curveTable[firstUnfilledBucket - 1]
                            : 0;

                        float difference = curveTable[i] - startingValue;
                        int bucketCount = i - firstUnfilledBucket;
                        for (int j = 0; j < bucketCount; j++)
                        {
                            curveTable[firstUnfilledBucket + j] = startingValue
                                + difference * ((j + 1) / (bucketCount + 1));
                        }

                        firstUnfilledBucket = -1;
                    }
                }

                return curveTable;
            }
        }

        /// <summary>
        /// Returns the curve-modified value.
        /// </summary>
        /// <param name="input">A value from 0 to 1.</param>
        public static float GetCurvedValue(PointF[] curveControlPoints, float[] curveTable, int curveTableResolution, float input)
        {
            // Assume linear graph if no points are given.
            if (curveControlPoints.Length == 0)
            {
                return input;
            }

            // Return value for a constant graph.
            if (curveControlPoints.Length == 1)
            {
                return curveControlPoints[0].Y;
            }

            // Returns value for perfect linear graph (to avoid needing a table, since this is so common).
            if (curveControlPoints.Length == 2 &&
                curveControlPoints[0].Equals(PointF.Empty)
                && curveControlPoints[1].X == 1
                && curveControlPoints[1].Y == 1)
            {
                return input;
            }

            // For curves that don't extend all the way left or right.
            if (curveControlPoints.Length > 0 && (
                input < curveControlPoints[0].X ||
                input > curveControlPoints[curveControlPoints.Length - 1].X))
            {
                return 0;
            }

            // For invalid values return 0.
            if (input < 0 || input > curveTableResolution)
            {
                return 0;
            }

            // Direct access without lerp.
            if (input == (int)input)
            {
                return curveTable[(int)input];
            }

            // Linear interpolate between values in the curve table.
            int lowerBound = (int)input;
            int upperBound = (int)Math.Ceiling(input);
            float amount = input - lowerBound;

            return curveTable[lowerBound] + amount * (curveTable[upperBound] - curveTable[lowerBound]);
        }

        /// <summary>
        /// Sets the curve resolution to the desired greater-than-zero number.
        /// </summary>
        public void SetCurveResolution(int resolution)
        {
            if (resolution < 1) { CurveTableResolution = 1; }
            CurveTable = GetCurveTable(curveControlPoints, CurveTableResolution);
        }
        #endregion

        #region Methods (event handlers)
        /// <summary>
        /// Handles key events like nudge, deletion, and cycling control points
        /// </summary>
        public void CurveGraph_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (EditMode == CurveEditMode.ViewOnly)
            {
                return;
            }

            if (e.KeyCode == Keys.C)
            {
                string copy = "";
                for (int i = 0; i < curveControlPoints.Length; i++)
                {
                    copy += curveControlPoints[i].X + ", " + curveControlPoints[i].Y + "\n";
                }
                Clipboard.SetText(copy);
            }

            // Nudges (and re-aligns) an active point via arrow keys.
            if (selectedControlPoint != -1 &&
                (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right ||
                e.KeyCode == Keys.Up || e.KeyCode == Keys.Down))
            {
                ref PointF pt = ref curveControlPoints[selectedControlPoint];

                if (!e.Control) // nudge
                {
                    if (e.KeyCode == Keys.Left) { pt.X = (float)Math.Round(pt.X * 200f - 2f) / 200f; }
                    else if (e.KeyCode == Keys.Right) { pt.X = (float)Math.Round(pt.X * 200f + 2f) / 200f; }
                    else if (e.KeyCode == Keys.Up) { pt.Y = (float)Math.Round(pt.Y * 200f + 2f) / 200f; }
                    else if (e.KeyCode == Keys.Down) { pt.Y = (float)Math.Round(pt.Y * 200f - 2f) / 200f; }
                }
                else // move node to the edge
                {
                    if (e.KeyCode == Keys.Left) { pt.X = 0; }
                    else if (e.KeyCode == Keys.Right) { pt.X = ClientRectangle.Width; }
                    else if (e.KeyCode == Keys.Up) { pt.Y = ClientRectangle.Height; }
                    else if (e.KeyCode == Keys.Down) { pt.Y = 0; }
                }

                pt.X = pt.X > 1 ? 1 : pt.X < 0 ? 0 : pt.X;
                pt.Y = pt.Y > 1 ? 1 : pt.Y < 0 ? 0 : pt.Y;

                Cursor.Position = PointToScreen(new Point(
                    (int)Math.Round(pt.X * ClientRectangle.Width),
                    (int)Math.Round(ClientRectangle.Height - (pt.Y * ClientRectangle.Height))));

                Refresh();
            }

            // Cycles through the selected active point.
            if (e.KeyCode == Keys.Space)
            {
                if (selectedControlPoint != -1)
                {
                    selectedControlPoint++;

                    if (selectedControlPoint == curveControlPoints.Length)
                    {
                        selectedControlPoint = 0;
                    }
                }
                else
                {
                    selectedControlPoint = 0;
                }

                PointF pt = curveControlPoints[selectedControlPoint];
                Point pointPos = new Point(
                    (int)Math.Round(pt.X * ClientRectangle.Width),
                    (int)Math.Round(ClientRectangle.Height - (pt.Y * ClientRectangle.Height)));

                Cursor.Position = PointToScreen(pointPos);
                mouseDownLoc = pointPos;

                Refresh();
            }

            // Deselects any selected control point.
            if (e.KeyCode == Keys.Escape && (selectedControlPoint != -1 || mouseDownLoc != null))
            {
                selectedControlPoint = -1;
                mouseDownLoc = null;
                SetCurvePoints(curveControlPoints);
            }
        }

        /// <summary>
        /// Inserts a control point on mouse release as applicable.
        /// </summary>
        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (EditMode == CurveEditMode.ViewOnly)
            {
                return;
            }

            // Adds a new point at the mouse location.
            if (EditMode == CurveEditMode.FullEdit && mouseDownLoc != null && selectedControlPoint == -1)
            {
                PointF newPoint = new PointF(e.X / (float)ClientRectangle.Width, (ClientRectangle.Height - e.Y) / (float)ClientRectangle.Height);
                newPoint.X = newPoint.X > 1 ? 1 : newPoint.X < 0 ? 0 : newPoint.X;
                newPoint.Y = newPoint.Y > 1 ? 1 : newPoint.Y < 0 ? 0 : newPoint.Y;

                float nearestValue = float.MaxValue;
                int index = -1;

                // Finds the point nearest to the left of the point to insert along the curve.
                for (int i = 0; i < curveControlPoints.Length; i++)
                {
                    float dist = newPoint.X - curveControlPoints[i].X;
                    if (dist >= 0 && dist < nearestValue)
                    {
                        nearestValue = dist;
                        index = i;

                        // sits vertical on an existing point, can't get closer so exit.
                        if (dist == 0)
                        {
                            break;
                        }
                    }
                }

                if (index == -1)
                {
                    curveControlPoints = curveControlPoints.Append(newPoint).ToArray();
                    selectedControlPoint = curveControlPoints.Length - 1;
                }
                else
                {
                    var list = curveControlPoints.ToList();
                    list.Insert(index + 1, newPoint);
                    curveControlPoints = list.ToArray();
                    selectedControlPoint = index + 1;
                }
            }

            SetCurvePoints(curveControlPoints);
            mouseDownLoc = null;
            selectedControlPoint = -1;
        }

        /// <summary>
        /// Moves the selected point or creates another if not dragging.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (EditMode == CurveEditMode.ViewOnly)
            {
                return;
            }

            mouseLoc = e.Location;

            // Selects the first eligible control point if the user clicks and drags one a small distance.
            if (mouseDownLoc != null && selectedControlPoint == -1 && Math.Sqrt(
                Math.Pow(mouseDownLoc.Value.X - mouseLoc.X, 2) +
                Math.Pow(mouseDownLoc.Value.Y - mouseLoc.Y, 2)) >= MOUSE_MOVE_THRESHOLD)
            {
                for (int i = 0; i < curveControlPoints.Length; i++)
                {
                    PointF location = new PointF(
                        curveControlPoints[i].X * ClientRectangle.Width,
                        ClientRectangle.Height - (curveControlPoints[i].Y * ClientRectangle.Height));

                    if (Math.Sqrt(
                        Math.Pow(location.X - mouseDownLoc.Value.X, 2) +
                        Math.Pow(location.Y - mouseDownLoc.Value.Y, 2)) < CONTROL_POINT_CLICK_RADIUS)
                    {
                        selectedControlPoint = i;
                        break;
                    }
                }

                // If no control point to drag was found, create one and select it if possible.
                if (selectedControlPoint == -1 && EditMode == CurveEditMode.FullEdit)
                {
                    PointF newPoint = new PointF(e.X / (float)ClientRectangle.Width, (ClientRectangle.Height - e.Y) / (float)ClientRectangle.Height);
                    newPoint.X = newPoint.X > 1 ? 1 : newPoint.X < 0 ? 0 : newPoint.X;
                    newPoint.Y = newPoint.Y > 1 ? 1 : newPoint.Y < 0 ? 0 : newPoint.Y;

                    float nearestValue = float.MaxValue;
                    int index = -1;

                    // Finds the point nearest to the left of the point to insert along the curve.
                    for (int i = 0; i < curveControlPoints.Length; i++)
                    {
                        float dist = newPoint.X - curveControlPoints[i].X;
                        if (dist >= 0 && dist < nearestValue)
                        {
                            nearestValue = dist;
                            index = i;

                            // sits vertical on an existing point, can't get closer so exit.
                            if (dist == 0)
                            {
                                break;
                            }
                        }
                    }

                    if (index == -1)
                    {
                        curveControlPoints = curveControlPoints.Append(newPoint).ToArray();
                        selectedControlPoint = curveControlPoints.Length - 1;
                    }
                    else
                    {
                        var list = curveControlPoints.ToList();
                        list.Insert(index + 1, newPoint);
                        curveControlPoints = list.ToArray();
                        selectedControlPoint = index + 1;
                    }
                }
            }

            // Moves the selected control point if one is set.
            else if (mouseDownLoc != null && selectedControlPoint != -1)
            {
                PointF newPoint = new PointF(e.X / (float)ClientRectangle.Width, (ClientRectangle.Height - e.Y) / (float)ClientRectangle.Height);
                newPoint.X = newPoint.X > 1 ? 1 : newPoint.X < 0 ? 0 : newPoint.X;
                newPoint.Y = newPoint.Y > 1 ? 1 : newPoint.Y < 0 ? 0 : newPoint.Y;
                curveControlPoints[selectedControlPoint] = newPoint;
            }

            Refresh();
        }

        /// <summary>
        /// Handles control point deletion and tracking mouse down state.
        /// </summary>
        public void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (EditMode != CurveEditMode.ViewOnly && e.Button == MouseButtons.Left)
            {
                mouseDownLoc = e.Location;
            }

            else if (EditMode == CurveEditMode.FullEdit && e.Button == MouseButtons.Right)
            {
                //Deletes the currently-dragged point.
                if (selectedControlPoint != -1)
                {
                    var list = curveControlPoints.ToList();
                    list.RemoveAt(selectedControlPoint);
                    curveControlPoints = list.ToArray();
                    SetCurvePoints(list);
                    selectedControlPoint = -1;
                    mouseDownLoc = null;
                }

                // Deletes the first eligible control point.
                else
                {
                    for (int i = 0; i < curveControlPoints.Length; i++)
                    {
                        PointF location = new PointF(
                            curveControlPoints[i].X * ClientRectangle.Width,
                            ClientRectangle.Height - (curveControlPoints[i].Y * ClientRectangle.Height));

                        if (Math.Sqrt(Math.Pow(location.X - e.X, 2) + Math.Pow(location.Y - e.Y, 2)) < CONTROL_POINT_CLICK_RADIUS)
                        {
                            var list = curveControlPoints.ToList();
                            list.RemoveAt(i);
                            curveControlPoints = list.ToArray();
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws the curve graph.
        /// </summary>
        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (EditMode == CurveEditMode.ViewOnly)
            {
                e.Graphics.Clear(Color.LightGray);
            } else
            {
                e.Graphics.Clear(Color.White);
            }

            // Draws the curve via its curve resolution.
            if (selectedControlPoint != -1 && CurveTable.Length > 0)
            {
                PointF[] modifiedPoints = new PointF[CurveTable.Length];
                for (int i = 0; i <CurveTable.Length; i++)
                {
                    modifiedPoints[i] = new PointF(
                        (i / (float)CurveTableResolution) * ClientRectangle.Width,
                        ClientRectangle.Height - (CurveTable[i] / CurveTableResolution * ClientRectangle.Height));
                }

                e.Graphics.DrawLines(Pens.Green, modifiedPoints);
            }

            // Draws a linear curve when none is provided.
            if (curveControlPoints.Length == 0)
            {
                e.Graphics.DrawLine(
                    Pens.Black,
                    new Point(0, ClientRectangle.Height),
                    new Point(ClientRectangle.Width, 0));
            }

            // Draws a constant curve for a single point.
            else if (curveControlPoints.Length == 1)
            {
                float y = ClientRectangle.Height - (curveControlPoints[0].Y * ClientRectangle.Height);

                e.Graphics.DrawLine(
                    Pens.Black,
                    new PointF(0, y),
                    new PointF(ClientRectangle.Width, y));
            }

            // Draws the curve if there are at least 2 control points.
            else
            {
                // Scale control points to fit range.
                PointF[] drawnPoints = curveControlPoints.Select((point) =>
                {
                    return new PointF(
                        point.X * ClientRectangle.Width,
                        ClientRectangle.Height - (point.Y * ClientRectangle.Height));
                }).ToArray();

                e.Graphics.DrawCurve(Pens.Black, drawnPoints);

                if (curveControlPoints[0].X > 0 &&
                    curveControlPoints[0].Y != 0)
                {
                    PointF startPoint = new PointF(
                        curveControlPoints[0].X * ClientRectangle.Width,
                        ClientRectangle.Height - (curveControlPoints[0].Y * ClientRectangle.Height));
                    e.Graphics.DrawLine(Pens.LightGray, new PointF(startPoint.X, ClientRectangle.Height), startPoint);
                }
                if (curveControlPoints[curveControlPoints.Length - 1].X < 1 &&
                    curveControlPoints[curveControlPoints.Length - 1].Y != 0)
                {
                    PointF endPoint = new PointF(
                        curveControlPoints[curveControlPoints.Length - 1].X * ClientRectangle.Width,
                        ClientRectangle.Height - (curveControlPoints[curveControlPoints.Length - 1].Y * ClientRectangle.Height));
                    e.Graphics.DrawLine(Pens.LightGray, new PointF(endPoint.X, ClientRectangle.Height), endPoint);
                }
            }

            // Draws the control points in non-view mode.
            if (EditMode != CurveEditMode.ViewOnly)
            {
                // Draws each control point for linear or greater graphs.
                if (curveControlPoints.Length != 0)
                {
                    for (int i = 0; i < curveControlPoints.Length; i++)
                    {
                        PointF location = new PointF(
                            curveControlPoints[i].X * ClientRectangle.Width,
                            ClientRectangle.Height - (curveControlPoints[i].Y * ClientRectangle.Height));

                        bool selected = selectedControlPoint == i || Math.Sqrt(
                            Math.Pow(location.X - mouseLoc.X, 2) +
                            Math.Pow(location.Y - mouseLoc.Y, 2)) < CONTROL_POINT_CLICK_RADIUS;

                        // Colors the node based on mouse proximity to indicate when in manipulation range.
                        Brush brush = selected
                            ? new SolidBrush(Color.FromArgb(128, 255, 0, 0))
                            : new SolidBrush(Color.FromArgb(128, 0, 0, 255));

                        int radius = selected ? CONTROL_POINT_RADIUS * 2 : CONTROL_POINT_RADIUS;

                        e.Graphics.FillEllipse(
                            brush,
                            location.X - radius,
                            location.Y - radius,
                            radius * 2,
                            radius * 2);
                    }
                }
            }
        }
        #endregion
    }
}
