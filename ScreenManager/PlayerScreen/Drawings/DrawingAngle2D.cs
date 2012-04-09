/*
Copyright © Joan Charmant 2008.
joan.charmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.

*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

using Kinovea.ScreenManager.Languages;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    [XmlType ("Angle")]
    public class DrawingAngle2D : AbstractDrawing, IKvaSerializable, IDecorable, IInitializable
    {
        #region Properties
        public DrawingStyle DrawingStyle
        {
        	get { return m_Style;}
        }
        public override InfosFading infosFading
        {
            get{ return m_InfosFading;}
            set{ m_InfosFading = value;}
        }
		public override DrawingCapabilities Caps
		{
			get { return DrawingCapabilities.ConfigureColor | DrawingCapabilities.Fading; }
		}
		public override List<ToolStripMenuItem> ContextMenu
		{
			get 
			{
				// Rebuild the menu to get the localized text.
				List<ToolStripMenuItem> contextMenu = new List<ToolStripMenuItem>();
        		
				m_mnuInvertAngle.Text = ScreenManagerLang.mnuInvertAngle;
        		contextMenu.Add(m_mnuInvertAngle);
        		
				return contextMenu; 
			}
		}
        #endregion

        #region Members
        private Point m_PointO;
        private Point m_PointA;
        private Point m_PointB;
        
        private AngleHelper m_AngleHelper = new AngleHelper(false, 40, false);
        private DrawingStyle m_Style;
        private StyleHelper m_StyleHelper = new StyleHelper();
        private InfosFading m_InfosFading;
        
        private ToolStripMenuItem m_mnuInvertAngle = new ToolStripMenuItem();
		
        private const int m_iDefaultBackgroundAlpha = 92;
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructor
        public DrawingAngle2D(Point o, Point a, Point b, long _iTimestamp, long _iAverageTimeStampsPerFrame, DrawingStyle _stylePreset)
        {
            // Core
            m_PointO = o;
            m_PointA = a;
            m_PointB = b;
            ComputeValues();

            // Decoration and binding to mini editors.
            m_StyleHelper.Bicolor = new Bicolor(Color.Empty);
            m_StyleHelper.Font = new Font("Arial", 12, FontStyle.Bold);
            if(_stylePreset != null)
            {
                m_Style = _stylePreset.Clone();
                BindStyle();    
            }
            
            // Fading
            m_InfosFading = new InfosFading(_iTimestamp, _iAverageTimeStampsPerFrame);

            // Context menu
            m_mnuInvertAngle.Click += new EventHandler(mnuInvertAngle_Click);
			m_mnuInvertAngle.Image = Properties.Drawings.angleinvert;
        }
        public DrawingAngle2D(XmlReader _xmlReader, PointF _scale, Metadata _parent)
            : this(Point.Empty, Point.Empty, Point.Empty, 0, 0, ToolManager.Angle.StylePreset.Clone())
        {
            ReadXml(_xmlReader, _scale);
        }
        #endregion

        #region AbstractDrawing Implementation
        public override void Draw(Graphics _canvas, CoordinateSystem _transformer, bool _bSelected, long _iCurrentTimestamp)
        {
            double fOpacityFactor = m_InfosFading.GetOpacityFactor(_iCurrentTimestamp);
            
            if (fOpacityFactor <= 0)
                return;
            
            Point pointO = _transformer.Transform(m_PointO);
            Point pointA = _transformer.Transform(m_PointA);
            Point pointB = _transformer.Transform(m_PointB);
            Rectangle boundingBox = _transformer.Transform(m_AngleHelper.BoundingBox);

            using(Pen penEdges = m_StyleHelper.GetBackgroundPen((int)(fOpacityFactor*255)))
            using(SolidBrush brushEdges = m_StyleHelper.GetBackgroundBrush((int)(fOpacityFactor*255)))
            using(SolidBrush brushFill = m_StyleHelper.GetBackgroundBrush((int)(fOpacityFactor*m_iDefaultBackgroundAlpha)))
            {
                // Disk section
                _canvas.FillPie(brushFill, boundingBox, (float)m_AngleHelper.Start, (float)m_AngleHelper.Sweep);
                _canvas.DrawPie(penEdges, boundingBox, (float)m_AngleHelper.Start, (float)m_AngleHelper.Sweep);
    
                // Edges
                _canvas.DrawLine(penEdges, pointO, pointA);
                _canvas.DrawLine(penEdges, pointO, pointB);
    
                // Handlers
                _canvas.DrawEllipse(penEdges, pointO.Box(3));
                _canvas.FillEllipse(brushEdges, pointA.Box(3));
                _canvas.FillEllipse(brushEdges, pointB.Box(3));
    			
                SolidBrush fontBrush = m_StyleHelper.GetForegroundBrush((int)(fOpacityFactor * 255));
                int angle = (int)Math.Round(m_AngleHelper.Sweep);
    			string label = angle.ToString() + "°";
                Font tempFont = m_StyleHelper.GetFont((float)_transformer.Scale);
    			SizeF labelSize = _canvas.MeasureString(label, tempFont);
                
                // Background
                float shiftx = (float)(_transformer.Scale * m_AngleHelper.TextPosition.X);
                float shifty = (float)(_transformer.Scale * m_AngleHelper.TextPosition.Y);
                PointF textOrigin = new PointF(shiftx + pointO.X - labelSize.Width / 2, shifty + pointO.Y - labelSize.Height / 2);
                RectangleF backRectangle = new RectangleF(textOrigin, labelSize);
                RoundedRectangle.Draw(_canvas, backRectangle, brushFill, tempFont.Height/4, false);
        
                // Text
    			_canvas.DrawString(label, tempFont, fontBrush, backRectangle.Location);
    			
    			tempFont.Dispose();
                fontBrush.Dispose();
            }
            
        }
        public override int HitTest(Point _point, long _iCurrentTimestamp)
        {
            // Convention: miss = -1, object = 0, handle = n.
            int iHitResult = -1;
            if (m_InfosFading.GetOpacityFactor(_iCurrentTimestamp) > 0)
            {
                if (m_PointO.Box(10).Contains(_point))
                    iHitResult = 1;
                else if (m_PointA.Box(10).Contains(_point))
                    iHitResult = 2;
                else if (m_PointB.Box(10).Contains(_point))
                    iHitResult = 3;
                else if (IsPointInObject(_point))
                    iHitResult = 0;
            }
            
            return iHitResult;
        }
        public override void MoveHandle(Point point, int handle, Keys modifiers)
        {
            int constraintAngleSubdivisions = 8; // (Constraint by 45° steps).
            switch (handle)
            {
                case 1:
                    m_PointO = point;
                    break;
                case 2:
                    if((modifiers & Keys.Shift) == Keys.Shift)
                    {
                        PointF result = GeometryHelper.GetPointAtClosestRotationStepCardinal(m_PointO, point, constraintAngleSubdivisions);
                        m_PointA = new Point((int)result.X, (int)result.Y);
                    }
                    else
                    {
                        m_PointA = point;
                    }
                    break;
                case 3:
                    if((modifiers & Keys.Shift) == Keys.Shift)
                    {
                        PointF result = GeometryHelper.GetPointAtClosestRotationStepCardinal(m_PointO, point, constraintAngleSubdivisions);
                        m_PointB = new Point((int)result.X, (int)result.Y);
                    }
                    else
                    {
                        m_PointB = point;
                    }
                    break;
                default:
                    break;
            }

            ComputeValues();
        }
        public override void MoveDrawing(int _deltaX, int _deltaY, Keys _ModifierKeys)
        {
            m_PointO.X += _deltaX;
            m_PointO.Y += _deltaY;

            m_PointA.X += _deltaX;
            m_PointA.Y += _deltaY;

            m_PointB.X += _deltaX;
            m_PointB.Y += _deltaY;

            ComputeValues();
        }
		#endregion
		
        public override string ToString()
        {
            return ScreenManagerLang.ToolTip_DrawingToolAngle2D;
        }
        public override int GetHashCode()
        {
            int iHash = m_PointO.GetHashCode();
            iHash ^= m_PointA.GetHashCode();
            iHash ^= m_PointB.GetHashCode();
            iHash ^= m_StyleHelper.GetHashCode();
            return iHash;
        }        
            
        #region KVA Serialization
        private void ReadXml(XmlReader _xmlReader, PointF _scale)
        {
            _xmlReader.ReadStartElement();
            
			while(_xmlReader.NodeType == XmlNodeType.Element)
			{
				switch(_xmlReader.Name)
				{
					case "PointO":
				        m_PointO = XmlHelper.ParsePoint(_xmlReader.ReadElementContentAsString());
                        break;
					case "PointA":
				        m_PointA = XmlHelper.ParsePoint(_xmlReader.ReadElementContentAsString());
                        break;
					case "PointB":
				        m_PointB = XmlHelper.ParsePoint(_xmlReader.ReadElementContentAsString());
                        break;
                    case "DrawingStyle":
						m_Style = new DrawingStyle(_xmlReader);
						BindStyle();
						break;
				    case "InfosFading":
						m_InfosFading.ReadXml(_xmlReader);
						break;
					default:
						string unparsed = _xmlReader.ReadOuterXml();
						log.DebugFormat("Unparsed content in KVA XML: {0}", unparsed);
						break;
				}
			}
			
			_xmlReader.ReadEndElement();
            
            m_PointO = new Point((int)((float)m_PointO.X * _scale.X), (int)((float)m_PointO.Y * _scale.Y));
            m_PointA = new Point((int)((float)m_PointA.X * _scale.X), (int)((float)m_PointA.Y * _scale.Y));
            m_PointB = new Point((int)((float)m_PointB.X * _scale.X), (int)((float)m_PointB.Y * _scale.Y));

            ComputeValues();
        }
        public void WriteXml(XmlWriter _xmlWriter)
		{
            _xmlWriter.WriteElementString("PointO", String.Format("{0};{1}", m_PointO.X, m_PointO.Y));
            _xmlWriter.WriteElementString("PointA", String.Format("{0};{1}", m_PointA.X, m_PointA.Y));
            _xmlWriter.WriteElementString("PointB", String.Format("{0};{1}", m_PointB.X, m_PointB.Y));
            
            _xmlWriter.WriteStartElement("DrawingStyle");
            m_Style.WriteXml(_xmlWriter);
            _xmlWriter.WriteEndElement();
            
            _xmlWriter.WriteStartElement("InfosFading");
            m_InfosFading.WriteXml(_xmlWriter);
            _xmlWriter.WriteEndElement();
            
            // Spreadsheet support.
        	_xmlWriter.WriteStartElement("Measure");        	
        	int angle = (int)Math.Floor(-m_AngleHelper.Sweep);        	
        	_xmlWriter.WriteAttributeString("UserAngle", angle.ToString());
        	_xmlWriter.WriteEndElement();
		}
        #endregion
        
        #region IInitializable implementation
        public void ContinueSetup(Point point, Keys modifiers)
		{
			MoveHandle(point, 2, modifiers);
		}
        #endregion
        
        #region Specific context menu
        private void mnuInvertAngle_Click(object sender, EventArgs e)
		{
        	Point temp = m_PointA;
        	m_PointA = m_PointB;
        	m_PointB = temp;
            ComputeValues();
        	CallInvalidateFromMenu(sender);
		}
        #endregion
        
        #region Lower level helpers
        private void BindStyle()
        {
            m_Style.Bind(m_StyleHelper, "Bicolor", "line color");
        }
        private void ComputeValues()
        {
            FixIfNull();
            m_AngleHelper.Update(m_PointO, m_PointA, m_PointB, 0);
        }
        private void FixIfNull()
        {
            if (m_PointA == m_PointO)
                m_PointA = new Point(m_PointO.X + 50, m_PointO.Y);

            if (m_PointB == m_PointO)
                m_PointB = new Point(m_PointO.X, m_PointO.Y - 50);
        }
        private bool IsPointInObject(Point _point)
        {
            return m_AngleHelper.Hit(_point);
        }
        #endregion
    }

       
}