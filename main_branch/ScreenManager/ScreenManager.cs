/*
Copyright � Joan Charmant 2008.
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

using AForge.Imaging.Filters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using Kinovea.Services;
using Kinovea.VideoFiles;

namespace Kinovea.ScreenManager
{
	
	#region Namespace wide delegates
	// To call for a repaint of a screen. Used in various places.
	public delegate void DelegateScreenInvalidate();
	
	// To execute a specific action when we 'undo' an 'add drawing' action. (change cursor, etc.)
	public delegate void DelegateDrawingUndrawn();
	#endregion
	
    public class ScreenManagerKernel : IKernel, IScreenHandler, ICommonControlsHandler, IMessageFilter
    {
        #region Imports Win32
        
        const int WM_KEYDOWN = 0x100;
        const int TIME_PERIODIC = 0x01;
        const int TIME_KILL_SYNCHRONOUS = 0x0100;

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint timeSetEvent(int msDelay, int msResolution, MMTimerEventHandler handler, ref int userCtx, int eventType);

        [DllImport("winmm.dll", SetLastError = true)]
        private static extern uint timeKillEvent(uint timerEventId);
        #endregion

        #region enums
        public enum SyncStep
        {
            Initial,
            StartingWait,
            BothPlaying,
            EndingWait
        }
        #endregion

        #region Internal delegates
        private delegate void MMTimerEventHandler(uint id, uint msg, ref int userCtx, int rsv1, int rsv2);
        #endregion

        #region Properties
        public UserControl UI
        {
            get { return _UI; }
            set { _UI = value; }
        }
        public ResourceManager resManager
        {
            get { return m_resManager; }
            set { m_resManager = value; }
        }
        public bool CancelLastCommand
        {
            get { return m_bCancelLastCommand; } // Unused.
            set { m_bCancelLastCommand = value; }
        }
        #endregion

        #region Members
        private UserControl _UI;
        private ResourceManager m_resManager;
        private bool m_bCancelLastCommand = false;

        //List of screens ( 0..n )
        public List<AbstractScreen> screenList = new List<AbstractScreen>();
        
        private bool m_bAdjustingImage = false;
        public AbstractScreen m_ActiveScreen = null;
        private bool m_bCommonControlsVisible = false;

        // Video Filters
        private AbstractVideoFilter[] m_VideoFilters;
        
        //Menus
        public ToolStripMenuItem mnuCloseFile = new ToolStripMenuItem();
        public ToolStripMenuItem mnuCloseFile2 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuSave = new ToolStripMenuItem();
 
        private ToolStripMenuItem mnuExportSpreadsheet = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportODF = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportMSXML = new ToolStripMenuItem();
        private ToolStripMenuItem mnuExportXHTML = new ToolStripMenuItem();
		private ToolStripMenuItem mnuLoadAnalysis = new ToolStripMenuItem();

        public ToolStripMenuItem mnuSwapScreens = new ToolStripMenuItem();
        public ToolStripMenuItem mnuToggleCommonCtrls = new ToolStripMenuItem();

        public ToolStripMenuItem mnuDeinterlace = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFormat = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFormatAuto = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFormatForce43 = new ToolStripMenuItem();
        private ToolStripMenuItem mnuFormatForce169 = new ToolStripMenuItem();
        public ToolStripMenuItem mnuMirror = new ToolStripMenuItem();
        public ToolStripMenuItem mnuGrid = new ToolStripMenuItem();
        public ToolStripMenuItem mnu3DPlane = new ToolStripMenuItem();

        #region Synchronization
        private MMTimerEventHandler m_DelegateMMTimerEventHandler;
        
        //private uint    m_IdMultimediaTimer = 0; // Timer servant � contr�ler l'�tat d'avancement de chaque vid�o pour prise de d�cision d'arr�t/relance.          
        
        private bool    m_bSynching;
        private bool 	m_bSyncMerging;				// true if blending each other videos. 
        private int     m_iSyncLag; 	            // Sync Lag in Frames, for static sync.
        private int     m_iSyncLagMilliseconds;		// Sync lag in Milliseconds, for dynamic sync.
        private bool 	m_bDynamicSynching;			// replace the common timer.
        
        // Static Sync Positions
        private int m_iCurrentFrame = 0;            // Current frame in trkFrame...
        private int m_iLeftSyncFrame = 0;           // Sync reference in the left video
        private int m_iRightSyncFrame = 0;          // Sync reference in the right video
        private int m_iMaxFrame = 0;                // Max du trkFrame

        // Dynamic Sync Flags.
        private bool m_bRightIsStarting = false;    // true when the video is between [0] and [1] frames.
        private bool m_bLeftIsStarting = false;
        private bool m_bLeftIsCatchingUp = false;   // CatchingUp is when the video is the only one left running,
        private bool m_bRightIsCatchingUp = false;  // heading towards end, the other video is waiting the lag.

        #endregion

        private bool m_bAllowKeyboardHandler;

        private List<ScreenManagerState> m_StoredStates  = new List<ScreenManagerState>();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion

        #region Constructor & initialization
        public ScreenManagerKernel()
        {
            log.Debug("Module Construction : ScreenManager.");

            Application.AddMessageFilter(this);
            
            //Gestion i18n
            resManager = new ResourceManager("Kinovea.ScreenManager.Languages.ScreenManagerLang", Assembly.GetExecutingAssembly());
            
            // Callbacks du MultimediaTimer.
            m_DelegateMMTimerEventHandler = new MMTimerEventHandler(MultimediaTimer_Tick);
            m_bAllowKeyboardHandler = true;

            UI = new ScreenManagerUserInterface(this);
            
            PlugDelegates();
            InitializeVideoFilters();
            
            // Registers our exposed functions to the DelegatePool.
            DelegatesPool dp = DelegatesPool.Instance();

            dp.LoadMovieInScreen = DoLoadMovieInScreen;
            dp.StopPlaying = DoStopPlaying;
            dp.DeactivateKeyboardHandler = DoDeactivateKeyboardHandler;
            dp.ActivateKeyboardHandler = DoActivateKeyboardHandler;
            dp.VideoProcessingDone = DoVideoProcessingDone;
        }
        private void PlugDelegates()
        {
            ((ScreenManagerUserInterface)this.UI).m_CallbackDropLoadMovie += new ScreenManagerUserInterface.CallbackDropLoadMovie(DropLoadMovie);
            ((ScreenManagerUserInterface)this.UI).m_ThumbsViewer.m_CallBackLoadMovie += new ScreenManagerUserInterface.CallbackDropLoadMovie(DropLoadMovie);
        }
        private void InitializeVideoFilters()
        {
        	// Creates Video Filters
        	m_VideoFilters = new AbstractVideoFilter[(int)VideoFilterType.NumberOfVideoFilters];
        	
        	m_VideoFilters[(int)VideoFilterType.AutoLevels] = new VideoFilterAutoLevels();
        	m_VideoFilters[(int)VideoFilterType.AutoContrast] = new VideoFilterContrast();
        	m_VideoFilters[(int)VideoFilterType.Sharpen] = new VideoFilterSharpen();
        	m_VideoFilters[(int)VideoFilterType.EdgesOnly] = new VideoFilterEdgesOnly();
			m_VideoFilters[(int)VideoFilterType.Mosaic] = new VideoFilterMosaic();
        	m_VideoFilters[(int)VideoFilterType.Reverse] = new VideoFilterReverse();
        	m_VideoFilters[(int)VideoFilterType.DebugSURF] = new VideoFilterDebugSURF();
        }
        public void PrepareScreen()
        {
        	// Prepare a screen to hold the command line argument file.
        	
        	IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
            CommandManager.Instance().LaunchUndoableCommand(caps);
            
            ICommand css = new CommandShowScreens(this);
            CommandManager.LaunchCommand(css);
            
            // Mettre � jour les menus
            OrganizeMenus();
        }
        #endregion

        #region IKernel Implementation
        public void BuildSubTree()
        {
            // No sub modules.
        }
        public void ExtendMenu(ToolStrip _menu)
        {
            #region File
            ToolStripMenuItem mnuCatchFile = new ToolStripMenuItem();
            mnuCatchFile.MergeIndex = 0; // (File)
            mnuCatchFile.MergeAction = MergeAction.MatchOnly;

            //Close File (1)
            mnuCloseFile.Tag = new ItemResourceInfo(resManager, "Generic_Close");
            mnuCloseFile.Text = ((ItemResourceInfo)mnuCloseFile.Tag).resManager.GetString(((ItemResourceInfo)mnuCloseFile.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuCloseFile.Enabled = false;
            mnuCloseFile.Click += new EventHandler(mnuCloseFileOnClick);

            mnuCloseFile.MergeIndex = 2;
            mnuCloseFile.MergeAction = MergeAction.Insert;

            //Close File (2)
            mnuCloseFile2.Tag = new ItemResourceInfo(resManager, "Generic_Close");
            mnuCloseFile2.Text = ((ItemResourceInfo)mnuCloseFile2.Tag).resManager.GetString(((ItemResourceInfo)mnuCloseFile2.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuCloseFile2.Enabled = false;
            mnuCloseFile2.Visible = false;
            mnuCloseFile2.Click += new EventHandler(mnuCloseFile2OnClick);

            mnuCloseFile2.MergeIndex = 3;
            mnuCloseFile2.MergeAction = MergeAction.Insert;

            // Save Analysis or Video
            mnuSave.Tag = new ItemResourceInfo(resManager, "mnuSave");
            mnuSave.Text = ((ItemResourceInfo)mnuSave.Tag).resManager.GetString(((ItemResourceInfo)mnuSave.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuSave.Click += new EventHandler(mnuSaveOnClick);
            mnuSave.ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S;
          
            mnuSave.MergeIndex = 5;
            mnuSave.MergeAction = MergeAction.Insert;

            //Export to Spreadsheet
            mnuExportSpreadsheet.Tag = new ItemResourceInfo(resManager, "mnuExportSpreadsheet");
            mnuExportSpreadsheet.Text = ((ItemResourceInfo)mnuExportSpreadsheet.Tag).resManager.GetString(((ItemResourceInfo)mnuExportSpreadsheet.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuExportSpreadsheet.MergeIndex = 6;
            mnuExportSpreadsheet.MergeAction = MergeAction.Insert;
            
            // - Calc (ODF)
            mnuExportODF.Tag = new ItemResourceInfo(resManager, "mnuExportODF");
            mnuExportODF.Text = ((ItemResourceInfo)mnuExportODF.Tag).resManager.GetString(((ItemResourceInfo)mnuExportODF.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuExportODF.Click += new EventHandler(mnuExportODF_OnClick);
            
            // - Microsft Excel (MS-XML)
            mnuExportMSXML.Tag = new ItemResourceInfo(resManager, "mnuExportMSXML");
            mnuExportMSXML.Text = ((ItemResourceInfo)mnuExportMSXML.Tag).resManager.GetString(((ItemResourceInfo)mnuExportMSXML.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuExportMSXML.Click += new EventHandler(mnuExportMSXML_OnClick);
            
            // - XHTML
            mnuExportXHTML.Tag = new ItemResourceInfo(resManager, "mnuExportXHTML");
            mnuExportXHTML.Text = ((ItemResourceInfo)mnuExportXHTML.Tag).resManager.GetString(((ItemResourceInfo)mnuExportXHTML.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuExportXHTML.Click += new EventHandler(mnuExportXHTML_OnClick);
            
            mnuExportSpreadsheet.DropDownItems.AddRange(new ToolStripItem[] { mnuExportODF, mnuExportMSXML, mnuExportXHTML });
            
            // Load Analysis
            mnuLoadAnalysis.Tag = new ItemResourceInfo(resManager, "mnuLoadAnalysis");
            mnuLoadAnalysis.Text = ((ItemResourceInfo)mnuLoadAnalysis.Tag).resManager.GetString(((ItemResourceInfo)mnuLoadAnalysis.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuLoadAnalysis.Click += new EventHandler(mnuLoadAnalysisOnClick);
            mnuLoadAnalysis.MergeIndex = 7;
            mnuLoadAnalysis.MergeAction = MergeAction.Insert;

            //---------------------------------
            //Organisation du sous menu File
            //---------------------------------
            ToolStripItem[] subFile = new ToolStripItem[] { mnuCloseFile, mnuCloseFile2, mnuSave, mnuExportSpreadsheet, mnuLoadAnalysis };
            mnuCatchFile.DropDownItems.AddRange(subFile);
            #endregion

            #region View
            ToolStripMenuItem mnuCatchScreens = new ToolStripMenuItem();
            mnuCatchScreens.MergeIndex = 2; // (Screens)
            mnuCatchScreens.MergeAction = MergeAction.MatchOnly;

            // One player
            ToolStripMenuItem mnuOnePlayer = new ToolStripMenuItem();
            mnuOnePlayer.Tag = new ItemResourceInfo(resManager, "mnuOnePlayer");
            mnuOnePlayer.Text = ((ItemResourceInfo)mnuOnePlayer.Tag).resManager.GetString(((ItemResourceInfo)mnuOnePlayer.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuOnePlayer.Click += new EventHandler(mnuOnePlayerOnClick);
            mnuOnePlayer.MergeAction = MergeAction.Append;

            // Two players
            ToolStripMenuItem mnuTwoPlayers = new ToolStripMenuItem();
            mnuTwoPlayers.Tag = new ItemResourceInfo(resManager, "mnuTwoPlayers");
            mnuTwoPlayers.Text = ((ItemResourceInfo)mnuTwoPlayers.Tag).resManager.GetString(((ItemResourceInfo)mnuTwoPlayers.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuTwoPlayers.Click += new EventHandler(mnuTwoPlayersOnClick);
            mnuTwoPlayers.MergeAction = MergeAction.Append;

			// One capture
            ToolStripMenuItem mnuOneCapture = new ToolStripMenuItem();
			mnuOneCapture.Tag = new ItemResourceInfo(resManager, "mnuOneCapture");
            mnuOneCapture.Text = ((ItemResourceInfo)mnuOneCapture.Tag).resManager.GetString(((ItemResourceInfo)mnuOneCapture.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuOneCapture.Click += new EventHandler(mnuOneCaptureOnClick);
            mnuOneCapture.MergeAction = MergeAction.Append;
            
            // Two captures
            ToolStripMenuItem mnuTwoCaptures = new ToolStripMenuItem();
            mnuTwoCaptures.Tag = new ItemResourceInfo(resManager, "mnuTwoCaptures");
            mnuTwoCaptures.Text = ((ItemResourceInfo)mnuTwoCaptures.Tag).resManager.GetString(((ItemResourceInfo)mnuTwoCaptures.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuTwoCaptures.Click += new EventHandler(mnuTwoCapturesOnClick);
            mnuTwoCaptures.MergeAction = MergeAction.Append;
			            
            // Two mixed
            ToolStripMenuItem mnuTwoMixed = new ToolStripMenuItem();
            mnuTwoMixed.Tag = new ItemResourceInfo(resManager, "mnuTwoMixed");
            mnuTwoMixed.Text = ((ItemResourceInfo)mnuTwoMixed.Tag).resManager.GetString(((ItemResourceInfo)mnuTwoMixed.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuTwoMixed.Click += new EventHandler(mnuTwoMixedOnClick);
            mnuTwoMixed.MergeAction = MergeAction.Append;
                        
            // Disabling all capture menus during dev.
			/*mnuOneCapture.Enabled = false;            
            mnuTwoCaptures.Enabled = false;
            mnuTwoMixed.Enabled = false;
 */
            //Swap - activ� seulement si DualFull ?
            mnuSwapScreens.Tag = new ItemResourceInfo(resManager, "mnuSwapScreens");
            mnuSwapScreens.Text = ((ItemResourceInfo)mnuSwapScreens.Tag).resManager.GetString(((ItemResourceInfo)mnuSwapScreens.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuSwapScreens.Enabled = false;
            mnuSwapScreens.Click += new EventHandler(mnuSwapScreensOnClick);
            mnuSwapScreens.MergeAction = MergeAction.Append;

            //Toggle Common Controls - activ� seulement si DualFull.
            mnuToggleCommonCtrls.Tag = new ItemResourceInfo(resManager, "mnuToggleCommonCtrls");
            mnuToggleCommonCtrls.Text = ((ItemResourceInfo)mnuToggleCommonCtrls.Tag).resManager.GetString(((ItemResourceInfo)mnuToggleCommonCtrls.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuToggleCommonCtrls.Enabled = false;
            mnuToggleCommonCtrls.ShortcutKeys = Keys.F5;
            mnuToggleCommonCtrls.Click += new EventHandler(mnuToggleCommonCtrlsOnClick);
            mnuToggleCommonCtrls.MergeAction = MergeAction.Append;
            
            //---------------------------------
            //Organisation du sous menu Screens
            //---------------------------------
            ToolStripItem[] subScreens = new ToolStripItem[] { 	mnuOnePlayer,
            														mnuTwoPlayers,
            														new ToolStripSeparator(),
            														mnuOneCapture, 
            														mnuTwoCaptures, 
            														mnuTwoMixed, 
            														new ToolStripSeparator(), 
            														mnuSwapScreens, 
            														mnuToggleCommonCtrls };
            mnuCatchScreens.DropDownItems.AddRange(subScreens);
            #endregion

            #region Image
            ToolStripMenuItem mnuCatchImage = new ToolStripMenuItem();
            mnuCatchImage.MergeIndex = 3; // (Image)
            mnuCatchImage.MergeAction = MergeAction.MatchOnly;
            
            // Deinterlace
            mnuDeinterlace.Tag = new ItemResourceInfo(resManager, "mnuDeinterlace");
            mnuDeinterlace.Text = ((ItemResourceInfo)mnuDeinterlace.Tag).resManager.GetString(((ItemResourceInfo)mnuDeinterlace.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuDeinterlace.Checked = false;
            mnuDeinterlace.ShortcutKeys = Keys.Control | Keys.D;
            mnuDeinterlace.Click += new EventHandler(mnuDeinterlaceOnClick);
            mnuDeinterlace.MergeAction = MergeAction.Append;
            
            
            #region Formats
            
            // Format Auto
            mnuFormatAuto.Tag = new ItemResourceInfo(resManager, "mnuFormatAuto");
            mnuFormatAuto.Text = ((ItemResourceInfo)mnuFormatAuto.Tag).resManager.GetString(((ItemResourceInfo)mnuFormatAuto.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuFormatAuto.Checked = true;
            mnuFormatAuto.Click += new EventHandler(mnuFormatAutoOnClick);
            mnuFormatAuto.MergeAction = MergeAction.Append;

            // Format 4:3
            mnuFormatForce43.Tag = new ItemResourceInfo(resManager, "mnuFormatForce43");
            mnuFormatForce43.Text = ((ItemResourceInfo)mnuFormatForce43.Tag).resManager.GetString(((ItemResourceInfo)mnuFormatForce43.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuFormatForce43.Click += new EventHandler(mnuFormatForce43OnClick);
            mnuFormatForce43.MergeAction = MergeAction.Append;

            // Format 16:9
            mnuFormatForce169.Tag = new ItemResourceInfo(resManager, "mnuFormatForce169");
            mnuFormatForce169.Text = ((ItemResourceInfo)mnuFormatForce169.Tag).resManager.GetString(((ItemResourceInfo)mnuFormatForce169.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuFormatForce169.Click += new EventHandler(mnuFormatForce169OnClick);
            mnuFormatForce169.MergeAction = MergeAction.Append;
            
            ToolStripSeparator mnuSepFormats = new ToolStripSeparator();
            
            // Image Format
            mnuFormat.Tag = new ItemResourceInfo(resManager, "mnuFormat");
            mnuFormat.Text = ((ItemResourceInfo)mnuFormat.Tag).resManager.GetString(((ItemResourceInfo)mnuFormat.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuFormat.MergeAction = MergeAction.Append;
            mnuFormat.DropDownItems.AddRange(new ToolStripItem[] { mnuFormatAuto, mnuSepFormats, mnuFormatForce43, mnuFormatForce169});
            
            #endregion
                        
            // Mirror
            mnuMirror.Tag = new ItemResourceInfo(resManager, "mnuMirror");
            mnuMirror.Text = ((ItemResourceInfo)mnuMirror.Tag).resManager.GetString(((ItemResourceInfo)mnuMirror.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuMirror.Checked = false;
            mnuMirror.ShortcutKeys = Keys.Control | Keys.M;
            mnuMirror.Click += new EventHandler(mnuMirrorOnClick);
            mnuMirror.MergeAction = MergeAction.Append;

            ToolStripSeparator mnuSep = new ToolStripSeparator();
            ToolStripSeparator mnuSep2 = new ToolStripSeparator();
            ToolStripSeparator mnuSep3 = new ToolStripSeparator();

            // Grid
            mnuGrid.Tag = new ItemResourceInfo(resManager, "mnuGrid");
            mnuGrid.Text = ((ItemResourceInfo)mnuGrid.Tag).resManager.GetString(((ItemResourceInfo)mnuGrid.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnuGrid.Checked = false;
            mnuGrid.ShortcutKeys = Keys.Control | Keys.G;
            mnuGrid.Click += new EventHandler(mnuGridOnClick);
            mnuGrid.MergeAction = MergeAction.Append;

            // 3D Plane
            mnu3DPlane.Tag = new ItemResourceInfo(resManager, "mnu3DPlane");
            mnu3DPlane.Text = ((ItemResourceInfo)mnu3DPlane.Tag).resManager.GetString(((ItemResourceInfo)mnu3DPlane.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            mnu3DPlane.Checked = false;
            mnu3DPlane.ShortcutKeys = Keys.Control | Keys.P;
            mnu3DPlane.Click += new EventHandler(mnu3DPlaneOnClick);
            mnu3DPlane.MergeAction = MergeAction.Append;

            ConfigureVideoFilterMenus(null, true);

            //---------------------------------
            //Organisation du sous menu Image
            //---------------------------------
            mnuCatchImage.DropDownItems.AddRange(new ToolStripItem[] 
													{ 
                                                   		mnuDeinterlace,
                                                   		mnuFormat,
                                                   		mnuSep, 
                                                   		m_VideoFilters[(int)VideoFilterType.AutoLevels].Menu,  
                                                   		m_VideoFilters[(int)VideoFilterType.AutoContrast].Menu,  
                                                   		m_VideoFilters[(int)VideoFilterType.Sharpen].Menu, 
                                                   		mnuSep2, 
                                                   		mnuMirror, 
                                                   		m_VideoFilters[(int)VideoFilterType.EdgesOnly].Menu, 
                                                   		mnuSep3, 
                                                   		mnuGrid, 
                                                   		mnu3DPlane});
            #endregion

            #region Motion
            ToolStripMenuItem mnuCatchMotion = new ToolStripMenuItem();
            mnuCatchMotion.MergeIndex = 4;
            mnuCatchMotion.MergeAction = MergeAction.MatchOnly;

            mnuCatchMotion.DropDownItems.AddRange(new ToolStripItem[] 
                                                  {  
                                                  		m_VideoFilters[(int)VideoFilterType.Mosaic].Menu,
                                                  		m_VideoFilters[(int)VideoFilterType.Reverse].Menu,
                                                  		m_VideoFilters[(int)VideoFilterType.DebugSURF].Menu});
            
            #endregion
            
            MenuStrip ThisMenu = new MenuStrip();
            ThisMenu.Items.AddRange(new ToolStripItem[] { mnuCatchFile, mnuCatchScreens, mnuCatchImage, mnuCatchMotion });
            ThisMenu.AllowMerge = true;

            ToolStripManager.Merge(ThisMenu, _menu);

            // No sub modules.
        }
        public void ExtendToolBar(ToolStrip _toolbar)
        {
            // TODO: Expose workspaces presets as buttons.

            //ToolStrip toolbar = new ToolStrip();

            /* D�roulement:
             * 1. Instanciation de l'item de menu.
             * 2. Association du texte du menu avec une resource.
             * 3. Affectation du contenu de la resource dans le texte.
             * 4. Affectation d'un Event Handler pour g�rer l'action � lancer.
             * 5. D�finition du mode d'insertion dans le menu g�n�ral.
             * 6. D�termination de l'index d'insertion dans le menu g�n�ral.
             * 7. Organisation des sous menus entre eux et avec le parent direct.
             * Note : les menus intraduisibles doivent avoir un .Tag == null.
            */


            /*
            ToolStripButton toolOnePlayer = new ToolStripButton();
            toolOnePlayer.Tag = new ItemResourceInfo(resManager, "toolOnePlayer", "mnuMonoPlayer");
            toolOnePlayer.Name                  = "toolOnePlayer";
            toolOnePlayer.DisplayStyle          = ToolStripItemDisplayStyle.Image;
            toolOnePlayer.Image                 = (System.Drawing.Image)(Kinovea.ScreenManager.Properties.Resources.MonoPlayer3);
            toolOnePlayer.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolOnePlayer.AutoSize              = true;
            toolOnePlayer.ToolTipText           = ((ItemResourceInfo)toolOnePlayer.Tag).resManager.GetString(((ItemResourceInfo)toolOnePlayer.Tag).strToolTipText, Thread.CurrentThread.CurrentUICulture); ;
            toolOnePlayer.Click += new EventHandler(mnuMonoPlayerOnClick);

            
            ToolStripButton toolTwoPlayers = new ToolStripButton();
            toolTwoPlayers.Tag = new ItemResourceInfo(resManager, "toolTwoPlayers", "mnuDoublePlayer");
            toolTwoPlayers.Name = "toolTwoPlayers";
            //toolTwoPlayers.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText; 
            toolTwoPlayers.DisplayStyle = ToolStripItemDisplayStyle.Image;
            //toolTwoPlayers.Text = ((ItemResourceInfo)toolTwoPlayers.Tag).resManager.GetString(((ItemResourceInfo)toolTwoPlayers.Tag).strText, Thread.CurrentThread.CurrentUICulture);
            //toolTwoPlayers.TextAlign = ContentAlignment.BottomCenter;
            //toolTwoPlayers.TextImageRelation = TextImageRelation.ImageAboveText;
            toolTwoPlayers.Image = (System.Drawing.Image)(Kinovea.ScreenManager.Properties.Resources.DualPlayer2);
            toolTwoPlayers.ImageTransparentColor = System.Drawing.Color.Magenta;
            toolTwoPlayers.AutoSize = true;
            toolTwoPlayers.ToolTipText = ((ItemResourceInfo)toolTwoPlayers.Tag).resManager.GetString(((ItemResourceInfo)toolTwoPlayers.Tag).strToolTipText, Thread.CurrentThread.CurrentUICulture); ;
            toolTwoPlayers.Click += new EventHandler(mnuDoublePlayerOnClick);
            



            //Organisation de la Toolbar
            ToolStripItem[] allButtons = new ToolStripItem[] { toolOnePlayer, toolTwoPlayers };
            toolbar.Items.AddRange(allButtons);
            
            toolbar.AllowMerge = true;

            toolStrips.Add(toolbar);
            */



            // No sub modules.


        }
        public void ExtendStatusBar(ToolStrip _statusbar)
        {
            // Nothing at this level.
            // No sub modules.
        }
        public void ExtendUI()
        {
            // No sub modules.
        }
        public void RefreshUICulture()
        {
            log.Debug("Refresh UI Culture at ScreenManager level.");
            OrganizeMenus();
            UpdateStatusBar();

            ((ScreenManagerUserInterface)this.UI).RefreshUICulture();

            // Screens.
            foreach (AbstractScreen screen in screenList)
            {
                screen.refreshUICulture();
            }

            ((ScreenManagerUserInterface)UI).DisplaySyncLag(m_iSyncLag);

            // No submodules.
        }
        public void CloseSubModules()
        {
            // No sub modules to close.
            // Close this module
            foreach (AbstractScreen screen in screenList)
            {
                screen.BeforeClose();
            }
        }
        #endregion
        
        #region IScreenHandler Implementation
        public void Screen_SetActiveScreen(AbstractScreen _ActiveScreen)
        {
            //---------------------------------------------------------------------------------
            // Cette fonction doit pouvoir �tre acc�d�e d�clench�e depuis les Screens.
            // Les screens contiennent un delegate avec ce prototype, on injecte cette fonction 
            // dans le delegate.
            //---------------------------------------------------------------------------------
            
            // /!\ Eviter d'appeller SetAsActiveScreen � tout bout de champ
            // La fonction OrganizeMenu est assez lourde au niveau de l'UI et peut
            // monopoliser la pile de messages windows.

            if (m_ActiveScreen != _ActiveScreen )
            {
                m_ActiveScreen = _ActiveScreen;
                
                if (screenList.Count > 1)
                {
                    m_ActiveScreen.DisplayAsActiveScreen(true);

                    // D�sactiver les autres
                    foreach (AbstractScreen screen in screenList)
                    {
                        if (screen != _ActiveScreen)
                        {
                            screen.DisplayAsActiveScreen(false);
                        }
                    }
                }
            }

            OrganizeMenus();

        }
        public void Screen_CloseAsked(AbstractScreen _SenderScreen)
        {
            _SenderScreen.BeforeClose();
            
            // Reorganise screens.
            // We leverage the fact that screens are always weel ordered relative to menus.
            if (_SenderScreen == screenList[0])
            {
                mnuCloseFileOnClick(null, EventArgs.Empty);
            }
            else
            {
                mnuCloseFile2OnClick(null, EventArgs.Empty);
            }
            
            PrepareSync(false);
        }
        public void Player_IsReady(PlayerScreen _screen, bool _bInitialisation)
        {
            // Appel� lors de changement de framerate.
            if (m_bSynching)
            {
            	log.Debug("Framerate of one screen changed, reset sync point.");
                SetSyncPoint(true);
            }
        }
        public void Player_SelectionChanged(PlayerScreen _screen, bool _bInitialization)
        {
        	PrepareSync(_bInitialization);
        }
        public void Player_ImageChanged(PlayerScreen _screen, Bitmap _image)
        {
        	if (m_bSynching)
            {
				// Transfer the image to the other screen.
        		if(m_bSyncMerging)
        		{
	        		foreach (AbstractScreen screen in screenList)
	                {
	                    if (screen != _screen && screen is PlayerScreen)
	                    {
	                    	Bitmap img = AForge.Imaging.Image.Clone(_image);
	                    	
	                    	// Mirroring is the only modification to the image we handle for now,
	                    	// no drawings, no magnifier, no zoom, etc.
	                    	if(((PlayerScreen)_screen).Mirrored)
	                    	{
	                    		Mirror mirrorFilter = new Mirror(false, true);
								mirrorFilter.ApplyInPlace( img );
	                    	}
	                    	
	                    	((PlayerScreen)screen).SyncMergeImage = img;
	                    }
	                }	
        		}

        		// Dynamic sync.
        		if(m_bDynamicSynching)
        		{
        			DynamicSync();
        		}
        		
        		
        		
        	}
        }
        public void Player_Reset(PlayerScreen _screen)
        {
        	// A screen was reset. (ex: a video was reloded in place).
        	// We need to also reset all the sync states.
        	PrepareSync(true);        	
        }

        public List<String> Capture_DeviceList()
        {
            List<String> result_list = new List<string>();

            FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo dev in videoDevices)
            {
                result_list.Add(dev.Name);
            }

            return result_list;
        }

        public bool Capture_TryDeviceConnection(CaptureScreen _screen, String _capture_device_name)
        {
        	bool bAtLeastOneDevice = false;
        	
        	// This function is called periodically by
        	// an empty Capture screen waiting for a device.
        	// We don't show an error if no free device is found.
        	log.Debug("Try to connect to a Capture Device.");
        	
        	// Check if there one and exactly one non-taken device and connect it to the screen.
        	FilterInfoCollection videoDevices = new FilterInfoCollection( FilterCategory.VideoInputDevice );

            if ( videoDevices.Count == 1 )
			{
        		// todo: check if not already taken.
        		_screen.FrameServer.SetDevice(videoDevices[0]);
        		bAtLeastOneDevice = true;
        	}
            else if (videoDevices.Count > 1 && _capture_device_name.Length > 0)
            {
                foreach (FilterInfo dev in videoDevices)
                {
                    if (dev.Name == _capture_device_name)
                    {
                        _screen.FrameServer.SetDevice(dev);
                    }
                }
                bAtLeastOneDevice = true;
            }
            else if (videoDevices.Count > 1)
            {
                // More than one device available.
                // todo: check which are already taken.
                // if only one left, connect it.
                // if several left, we'll need to ask the user.
                /*
                                MessageBox.Show(
                                    "more than one capture device",
                                    "debug",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Exclamation);
                bAtLeastOneDevice = true;*/
            }
        	
        	_screen.PostTryConnection();
        	
        	return bAtLeastOneDevice;
        }
        #endregion
        
        #region ICommonControlsHandler Implementation
        public void CommonCtrl_GotoFirst()
        {
        	DoStopPlaying();
        	
        	if (m_bSynching)
            {
                m_iCurrentFrame = 0;
                OnCommonPositionChanged(m_iCurrentFrame);
                ((ScreenManagerUserInterface)UI).UpdateTrkFrame(m_iCurrentFrame);
                
            }
            else
            {
                // Demander un GotoFirst � tout le monde
                foreach (AbstractScreen screen in screenList)
                {
                    if (screen is PlayerScreen)
                    {
                        ((PlayerScreen)screen).m_PlayerScreenUI.buttonGotoFirst_Click(null, EventArgs.Empty);
                    }
                }
            }	
        }
        public void CommonCtrl_GotoPrev()
        {
        	DoStopPlaying();
        	
        	if (m_bSynching)
            {
                if (m_iCurrentFrame > 0)
                {
                    m_iCurrentFrame--;
                    OnCommonPositionChanged(m_iCurrentFrame);
                    ((ScreenManagerUserInterface)UI).UpdateTrkFrame(m_iCurrentFrame);
                }
            }
            else
            {
                // Demander un GotoPrev � tout le monde
                foreach (AbstractScreen screen in screenList)
                {
                    if (screen.GetType().FullName.Equals("Kinovea.ScreenManager.PlayerScreen"))
                    {
                        ((PlayerScreen)screen).m_PlayerScreenUI.buttonGotoPrevious_Click(null, EventArgs.Empty);
                    }
                }
            }	
        }
		public void CommonCtrl_GotoNext()
        {
			DoStopPlaying();
			
        	if (m_bSynching)
            {
                if (m_iCurrentFrame < m_iMaxFrame)
                {
                    m_iCurrentFrame++;
                    OnCommonPositionChanged(-1);
                    ((ScreenManagerUserInterface)UI).UpdateTrkFrame(m_iCurrentFrame);
                }
            }
            else
            {
                // Demander un GotoNext � tout le monde
                foreach (AbstractScreen screen in screenList)
                {
                    if (screen.GetType().FullName.Equals("Kinovea.ScreenManager.PlayerScreen"))
                    {
                        ((PlayerScreen)screen).m_PlayerScreenUI.buttonGotoNext_Click(null, EventArgs.Empty);
                    }
                }
            }	
        }
		public void CommonCtrl_GotoLast()
        {
			DoStopPlaying();
        	
			if (m_bSynching)
            {
                m_iCurrentFrame = m_iMaxFrame;
                OnCommonPositionChanged(m_iCurrentFrame);
                ((ScreenManagerUserInterface)UI).UpdateTrkFrame(m_iCurrentFrame);
                
            }
            else
            {
                // Demander un GotoLast � tout le monde
                foreach (AbstractScreen screen in screenList)
                {
                    if (screen is PlayerScreen)
                    {
                        ((PlayerScreen)screen).m_PlayerScreenUI.buttonGotoLast_Click(null, EventArgs.Empty);
                    }
                }
            }	
        }
		public void CommonCtrl_Play()
        {
			bool bPlaying = ((ScreenManagerUserInterface)UI).ComCtrls.Playing;
        	if (m_bSynching)
            {
                if (bPlaying)
                {
                    int frameInterval = Math.Min(((PlayerScreen)screenList[0]).FrameInterval/2, ((PlayerScreen)screenList[1]).FrameInterval/2);
										
					// On play, simply launch the dynamic sync.
					// It will handle which video can start right away.
					StartDynamicSync(frameInterval);
                }
                else
                {
                    StopDynamicSync();
                    m_bLeftIsStarting = false;
                    m_bRightIsStarting = false;
                }
            }

        	// On stop, propagate the call to screens.
        	if(!bPlaying)
        	{	
        		if(screenList[0] is PlayerScreen)
	        		EnsurePause(0);
        		
        		if(screenList[1] is PlayerScreen)
        			EnsurePause(1);
        	}
        }
		public void CommonCtrl_Swap()
        {
        	mnuSwapScreensOnClick(null, EventArgs.Empty);	
        }
		public void CommonCtrl_Sync()
        {
        	if (m_bSynching && screenList.Count == 2)
            {
                // Mise � jour : m_iLeftSyncFrame, m_iRightSyncFrame, m_iSyncLag, m_iCurrentFrame. m_iMaxFrame.
                log.Debug("Sync point change.");
                SetSyncPoint(false);
                SetSyncLimits();

                // Mise � jour du trkFrame.
                ((ScreenManagerUserInterface)UI).SetupTrkFrame(0, m_iMaxFrame, m_iCurrentFrame);

                // Mise � jour des Players.
                OnCommonPositionChanged(m_iCurrentFrame);

                // debug
                ((ScreenManagerUserInterface)UI).DisplaySyncLag(m_iSyncLag);
            }
        }
		public void CommonCtrl_Merge()
        {
        	if (m_bSynching && screenList.Count == 2)
            {
        		m_bSyncMerging = ((ScreenManagerUserInterface)UI).ComCtrls.SyncMerging;
        		log.Debug(String.Format("SyncMerge videos is now {0}", m_bSyncMerging.ToString()));
        		
        		// This will also do a full refresh, and triggers Player_ImageChanged().
        		((PlayerScreen)screenList[0]).SyncMerge = m_bSyncMerging;
        		((PlayerScreen)screenList[1]).SyncMerge = m_bSyncMerging;
        	}
        }
       	public void CommonCtrl_PositionChanged(long _iPosition)
       	{
       		// Manual static sync.
       		if (m_bSynching)
            {
                StopDynamicSync();
                
                EnsurePause(0);
                EnsurePause(1);

                ((ScreenManagerUserInterface)UI).DisplayAsPaused();

                m_iCurrentFrame = (int)_iPosition;
                OnCommonPositionChanged(m_iCurrentFrame);
            }	
       	}
		#endregion
        
        #region IMessageFilter Implementation
        public bool PreFilterMessage(ref Message m)
        {
            //----------------------------------------------------------------------------
            // Attention au niveau des performances avec cette fonction
            // car du coup tous les WM_XXX windows passent par l�
            // WM_PAINT, WM_MOUSELEAVE de tous les contr�les, etc...
            // Plus on la place haut dans la hi�rarchie, plus elle plombe les perfs.
            //
            // Les actions de ce KeyHandler n'affectent pour la plupart que l'ActiveScreen
            // (sauf en mode DualScreen)
            //
            // Si cette fonction interf�re avec d'autres parties 
            // (car elle red�finie return, space, etc.) utiliser le delegate pool avec 
            // DeactivateKeyboardHandler et ActivateKeyboardHandler
            //----------------------------------------------------------------------------

            bool bWasHandled = false;
			ScreenManagerUserInterface smui = UI as ScreenManagerUserInterface;
            	
			if (m_bAllowKeyboardHandler && smui != null)
            {
                m_bCommonControlsVisible = !smui.splitScreensPanel.Panel2Collapsed;
                bool bThumbnailsViewerVisible = smui.m_ThumbsViewer.Visible;

                if ( (m.Msg == WM_KEYDOWN)  && 
                     (!m_bAdjustingImage)   &&
                     ((screenList.Count > 0 && m_ActiveScreen != null) || (bThumbnailsViewerVisible)))
                {
                    Keys keyCode = (Keys)(int)m.WParam & Keys.KeyCode;

                    switch (keyCode)
                    {
                    	case Keys.Delete:
                    	case Keys.Add:
                    	case Keys.Subtract:
                    	case Keys.F2:
                    	case Keys.F7:
                            {
                    			//------------------------------------------------
                    			// These keystrokes impact only the active screen.
                    			//------------------------------------------------
                    			if(!bThumbnailsViewerVisible)
                    			{       
									bWasHandled = m_ActiveScreen.OnKeyPress(keyCode);
                    			}
								else
                    			{
                    				bWasHandled = smui.m_ThumbsViewer.OnKeyPress(keyCode);
                    			}
                    			break;
                            }
                    	case Keys.Escape:
                    	case Keys.F6:
                    	case Keys.F11:
                    	case Keys.Down:
                    	case Keys.Up:
                            {
                    			//---------------------------------------------------
                    			// These keystrokes impact each screen independently.
                    			//---------------------------------------------------
                    			if(!bThumbnailsViewerVisible)
                    			{
	                                foreach (AbstractScreen abScreen in screenList)
	                                {
	                                    bWasHandled = abScreen.OnKeyPress(keyCode);
	                                }
                    			}
                    			else
                    			{
                    				bWasHandled = smui.m_ThumbsViewer.OnKeyPress(keyCode);
                    			}
                                break;
                            }
                        case Keys.Space:
                    	case Keys.Return:
                    	case Keys.Left:
                    	case Keys.Right:
                    	case Keys.End:
                    	case Keys.Home:
                            {
                                //---------------------------------------------------
                    			// These keystrokes impact both screens as a whole.
                    			//---------------------------------------------------
                               	if(!bThumbnailsViewerVisible)
                    			{
                               		if (screenList.Count == 2)
	                                {
                               			if(m_bCommonControlsVisible)
                               			{
                               				bWasHandled = OnKeyPress(keyCode);
                               			}
                               			else
                               			{
                               				bWasHandled = m_ActiveScreen.OnKeyPress(keyCode);	
                               			}
	                                }
	                                else if(screenList.Count == 1)
	                                {
	                                	bWasHandled = screenList[0].OnKeyPress(keyCode);
	                                }	
                               	}
                    			else
                    			{
                    				bWasHandled = smui.m_ThumbsViewer.OnKeyPress(keyCode);	
                    			}
                                break;
                            }
                    	//-------------------------------------------------
                    	// All the remaining keystrokes impact both screen, 
                    	// even if the common controls aren't visible.
                    	//-------------------------------------------------
                        case Keys.PageUp:
                    	case Keys.PageDown:
                            {
                    			// Change active screen.
                    			if(!bThumbnailsViewerVisible)
                    			{
                    				if(m_bSynching)
                               		{
                    					ActivateOtherScreen();
                    					bWasHandled = true;
                    				}
                    			}
                    			else
                    			{
                    				bWasHandled = smui.m_ThumbsViewer.OnKeyPress(keyCode);	
                    			}
                    			break;
                    		}
                        case Keys.F8:
                        	{
	                            // Go to sync frame. 
	                            if(!bThumbnailsViewerVisible)
	                    		{
	                            	if(m_bSynching)
                               		{
		                                if (m_iSyncLag > 0)
		                                {
		                                    m_iCurrentFrame = m_iRightSyncFrame;
		                                }
		                                else
		                                {
		                                    m_iCurrentFrame = m_iLeftSyncFrame;
		                                }
		
		                                // Update
		                                OnCommonPositionChanged(m_iCurrentFrame);
		                                smui.UpdateTrkFrame(m_iCurrentFrame);
		                                bWasHandled = true;
	                            	}
	                            }
	                            else
                    			{
                    				bWasHandled = smui.m_ThumbsViewer.OnKeyPress(keyCode);	
                    			}
	                            break;
                        	}
                        case Keys.F9:
                            {
                                //---------------------------------------
                                // Fonctions associ�es : 
                                // Resynchroniser apr�s d�placement individuel
                                //---------------------------------------
                               	if(!bThumbnailsViewerVisible)
                                {
                               		if(m_bSynching)
                               		{
                               			SyncCatch();
                               			bWasHandled = true;
                               		}
                                }
                               	else
                    			{
                    				bWasHandled = smui.m_ThumbsViewer.OnKeyPress(keyCode);	
                    			}
                                break;
                            }
                        default:
                            break;
                    }
                }
            }

            return bWasHandled;
        }
        #endregion
        
        public void UpdateStatusBar()
        {
            //------------------------------------------------------------------
            // Fonction appell�e sur RefreshUiCulture, CommandShowScreen (dans le ScreenManager)
            // 
            // et appelant le module sup�rieur (Supervisor)  
            //
            // Mettre � jour les infos de la status bar.
            // Fabriquer la cha�ne qui ira dans l'espace d�di� au ScreenManager.
            //------------------------------------------------------------------

            String StatusString = "";

            switch(screenList.Count)
            {
                case 1:
                    {
            			PlayerScreen ps = screenList[0] as PlayerScreen;
            			if(ps != null)
            			{
            				if(ps.Full)
                            {
                                StatusString += ps.FileName;
                            }
                            else
                            {
                                // Un seul �cran de lecture, avec rien dedans.
                                StatusString += StatusString += resManager.GetString("statusEmptyScreen", Thread.CurrentThread.CurrentUICulture); 
                            }            				
            			}

                        break;
                    }
                
                case 2:
                    {
                        PlayerScreen ps0 = screenList[0] as PlayerScreen;
            			if(ps0 != null)
            			{
            				if(ps0.Full)
                            {
                                StatusString += ps0.FileName;
                            }
                            else
                            {
                                // Ecran de gauche en lecture, avec rien dedans.
                                StatusString += StatusString += resManager.GetString("statusEmptyScreen", Thread.CurrentThread.CurrentUICulture); ;
                            }	
            			}

            			PlayerScreen ps1 = screenList[1] as PlayerScreen;
            			if(ps1 != null)
            			{
            				StatusString += " | ";
                            
                            if (ps1.Full)
                            {
                                StatusString += ps1.FileName;
                            }
                            else
                            {
                                // Ecran de droite en lecture, avec rien dedans.
                                StatusString += resManager.GetString("statusEmptyScreen", Thread.CurrentThread.CurrentUICulture);
                            }	
            			}
                        break;
                    }
                default:
                    break;

            }

            DelegatesPool dp = DelegatesPool.Instance();
            if (dp.UpdateStatusBar != null)
            {
                dp.UpdateStatusBar(StatusString);
            }
        }
        public void OrganizeMenus()
        {
            DoOrganizeMenu();
        }
        private void DoOrganizeMenu()
        {
        	// Show / hide menus depending on state of active screen
        	// and global screen configuration.
        	
            #region Menus depending only on the state of the active screen
            bool bActiveScreenIsEmpty = false;
            if (m_ActiveScreen != null && screenList.Count > 0)
            {
            	PlayerScreen player = m_ActiveScreen as PlayerScreen;
                if (player != null)
                {
                    // 1. Video is loaded : save-able and analysis is loadable.
                    if (player.Full)
                    {
                        mnuSave.Enabled = true;
                       	mnuExportSpreadsheet.Enabled = player.FrameServer.Metadata.HasData;
                        mnuExportODF.Enabled = player.FrameServer.Metadata.HasData;
                        mnuExportMSXML.Enabled = player.FrameServer.Metadata.HasData;
                        mnuExportXHTML.Enabled = player.FrameServer.Metadata.HasData;
                        mnuLoadAnalysis.Enabled = true;
                        mnuDeinterlace.Checked = player.Deinterlaced;
                        ConfigureImageFormatMenus(player);
                        mnuMirror.Checked = player.Mirrored;
                        mnuGrid.Checked = player.ShowGrid;
                        mnu3DPlane.Checked = player.Show3DPlane;

                        // Video Filters menus
                        ConfigureVideoFilterMenus(player, false);
                    }
                    else
                    {
                        // Active screen is an empty player screen.
                        bActiveScreenIsEmpty = true;
                    }
                }
                else
                {
                    // Active screen is not a PlayerScreen.
                    bActiveScreenIsEmpty = true;
                }
            }
            else
            {
                // No active screen. ( = no screens)
                bActiveScreenIsEmpty = true;
            }

            if (bActiveScreenIsEmpty)
            {
                mnuLoadAnalysis.Enabled = false;
                mnuSave.Enabled = false;
				mnuDeinterlace.Checked = false;
				ConfigureImageFormatMenus(null);
				mnuMirror.Checked = false;
                mnuGrid.Checked = false;
                mnu3DPlane.Checked = false;
                
                mnuExportSpreadsheet.Enabled = false;
                mnuExportODF.Enabled = false;
                mnuExportMSXML.Enabled = false;
                mnuExportXHTML.Enabled = false;
				
                // Video Filters menus
				ConfigureVideoFilterMenus(null, true);
            }
            #endregion

            #region Menus depending on the specifc screen configuration
            // File
            mnuCloseFile.Visible  = false;
            mnuCloseFile.Enabled  = false;
            mnuCloseFile2.Visible = false;
            mnuCloseFile2.Enabled = false;
            string strClosingText = ((ItemResourceInfo)mnuCloseFile.Tag).resManager.GetString(((ItemResourceInfo)mnuCloseFile.Tag).strText, Thread.CurrentThread.CurrentUICulture);

            bool bAllScreensEmpty = false;
            switch (screenList.Count)
            {
                case 0:

                    // No screens at all.

                    mnuSwapScreens.Enabled        = false;
                    mnuToggleCommonCtrls.Enabled  = false;

                    bAllScreensEmpty = true;
                    break;

                case 1:
                    
                    // Only one screen

                    mnuSwapScreens.Enabled        = false;
                    mnuToggleCommonCtrls.Enabled  = false;

                    
                    if(!screenList[0].Full)
                    {
                    	bAllScreensEmpty = true;	
                    }
                    else if(screenList[0] is PlayerScreen)
                    {
                    	// Only screen is an full PlayerScreen.
                        //strClosingText = strClosingText;// + " - " + ((PlayerScreen)screenList[0]).FileName;
                        mnuCloseFile.Text = strClosingText;
                        mnuCloseFile.Enabled = true;
                        mnuCloseFile.Visible = true;

                        mnuCloseFile2.Visible = false;
                        mnuCloseFile2.Enabled = false;
                    }
                    else if(screenList[0] is CaptureScreen)
                    {
                    	bAllScreensEmpty = true;	
                    }
                    break;

                case 2:

                    // Two screens

                    mnuSwapScreens.Enabled = true;
                    mnuToggleCommonCtrls.Enabled = true;
                    
                    m_bCommonControlsVisible = !((ScreenManagerUserInterface)UI).splitScreensPanel.Panel2Collapsed; 

                    // Left Screen
                    if (screenList[0] is PlayerScreen)
                    {
                        if (screenList[0].Full)
                        {
                            bAllScreensEmpty = false;
                            
                            string strCompleteClosingText = strClosingText + " - " + ((PlayerScreen)screenList[0]).FileName;
                            mnuCloseFile.Text = strCompleteClosingText;
                            mnuCloseFile.Enabled = true;
                            mnuCloseFile.Visible = true;
                        }
                        else
                        {
                            // Left screen is an empty PlayerScreen.
                            // Global emptiness might be changed below.
                            bAllScreensEmpty = true;
                        }
                    }
                    else if(screenList[0] is CaptureScreen)
                    {
                        // Global emptiness might be changed below.
                        bAllScreensEmpty = true;
                    }

                    // Right Screen.
                    if (screenList[1] is PlayerScreen)
                    {
                    	if (screenList[1].Full)
                        {
                            bAllScreensEmpty = false;
                            
                            string strCompleteClosingText = strClosingText + " - " + ((PlayerScreen)screenList[1]).FileName;
                            mnuCloseFile2.Text = strCompleteClosingText;
                            mnuCloseFile2.Enabled = true;
                            mnuCloseFile2.Visible = true;
                        }
                        else
                        {
                            // Ecran de droite en lecture, avec rien dedans.
                            // Si l'�cran de gauche �tait �galement vide, bEmpty reste � true.
                            // Si l'�cran de gauche �tait plein, bEmpty reste � false.
                        }
                    }
                    else if (screenList[1] is CaptureScreen)
                    {
                        // Ecran de droite en capture.
                        // Si l'�cran de gauche �tait �galement vide, bEmpty reste � true.
                        // Si l'�cran de gauche �tait plein, bEmpty reste � false.
                    }
                    break;

                default:
                    // KO.
                    mnuSwapScreens.Enabled       = false;
                    mnuToggleCommonCtrls.Enabled = false;
                    bAllScreensEmpty = true;
                    break;
            }

            if (bAllScreensEmpty)
            {
                // No screens at all, or all screens empty => 1 menu visible but disabled.

                mnuCloseFile.Text = strClosingText;
                mnuCloseFile.Visible = true;
                mnuCloseFile.Enabled = false;

                mnuCloseFile2.Visible = false;
            }
            #endregion

        }
        private void ConfigureVideoFilterMenus(PlayerScreen _player, bool _bDisableAll)
        {
			// determines if any given video filter menu should be
			// visible, enabled, checked...
        	
        	//----------------------------------------------------------
        	// 1. Is a given menu enabled ? (analysis mode/regular mode)
        	//----------------------------------------------------------
        	bool bEnable = false;
        	
        	if(!_bDisableAll && _player != null)
        	{
        		bEnable = _player.IsInAnalysisMode;
        	}
        	        	
    		foreach(AbstractVideoFilter vf in m_VideoFilters)
        	{
        		vf.Menu.Enabled = bEnable;
        	}
            
            // Associate the input frames
            if(bEnable)
            {
            	List<DecompressedFrame> frameList = _player.FrameServer.VideoFile.FrameList;
	            
            	foreach(AbstractVideoFilter vf in m_VideoFilters)
            	{
            		vf.FrameList = frameList;
            	}
            }

            //----------------------------------------------------------
            // 2. Is a given menu visible ?
            //----------------------------------------------------------
            foreach(AbstractVideoFilter vf in m_VideoFilters)
        	{
            	if(vf.Experimental)
            	{
            		// Experimental filters = depends on current release type.
            		vf.Menu.Visible = PreferencesManager.ExperimentalRelease;
            	}
            	else
            	{
            		// Production filters = always visible.
            		vf.Menu.Visible = true;
            	}
        	}
                      
            //----------------------------------------------------------
            // 3. Is a given boolean menu checked ?
        	//----------------------------------------------------------
            
        	// Uncheck all togglable menus
        	foreach(AbstractVideoFilter vf in m_VideoFilters)
        	{
        		vf.Menu.Checked = false;
        	}
        	
        	if(_player != null)
        	{
        		if(_player.DrawtimeFilterType > -1)
        		{
        			m_VideoFilters[_player.DrawtimeFilterType].Menu.Checked = true;
        		}
        	}
        }
        private void ConfigureImageFormatMenus(PlayerScreen _player)
        {
        	mnuFormatAuto.Checked = false;
        	mnuFormatForce43.Checked = false;
        	mnuFormatForce169.Checked = false;
        	
        	if(_player != null)
        	{
	        	switch(_player.AspectRatio)
	        	{
	        		case VideoFiles.AspectRatio.Force43:
	        			mnuFormatForce43.Checked = true;
	        			break;
	        		case VideoFiles.AspectRatio.Force169:
	        			mnuFormatForce169.Checked = true;
	        			break;
	        		case VideoFiles.AspectRatio.AutoDetect:
	        		default:
	        			mnuFormatAuto.Checked = true;
	        			break;
	        	}
        	}
        	else
        	{
        		mnuFormatAuto.Checked = true;
        	}
        }
        
        #region Menus events handlers

        #region File
        private void mnuCloseFileOnClick(object sender, EventArgs e)
        {
            //--------------------------------------------------------------------
            // Dans cet event Handler, on ferme toujours le premier �cran.
            // Si on a pu cliquer, c'est qu'il y a forc�ment une vid�o de charg�e 
            // dans le premier �cran.
            //--------------------------------------------------------------------
            // Supprimer explicitement l'�cran de la liste.
            IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
            CommandManager cm = CommandManager.Instance();
            cm.LaunchUndoableCommand(crs);

            //----------------------------------
            // doublon avec commandshowscreens ?
            //----------------------------------
            
            if (!m_bCancelLastCommand)
            {
                // Cleanup screen panel.
                ((ScreenManagerUserInterface)UI).splitScreens.Panel1.Controls.Clear();

                // ?
                
                switch (screenList.Count)
                {
                    case 1:
                        // Plus aucun �cran.
                        ((ScreenManagerUserInterface)UI).pnlScreens.Visible = false;
                        ((ScreenManagerUserInterface)UI).AllowDrop = true;
                        ((ScreenManagerUserInterface)UI).splitScreens.Panel1.AllowDrop = false;
                        ((ScreenManagerUserInterface)UI).splitScreens.Panel2.AllowDrop = false;
                        m_ActiveScreen = null;
                        break;

                    case 2:
                        // Dupliquer le second �cran dans le premier...
                        ((ScreenManagerUserInterface)UI).splitScreens.Panel1.Controls.Add(((ScreenManagerUserInterface)UI).splitScreens.Panel2.Controls[0]);

                        // Supprimer le second.
                        ((ScreenManagerUserInterface)UI).splitScreens.Panel2.Controls.Clear();

                        // Ne garder que le premier de visible.
                        ((ScreenManagerUserInterface)UI).splitScreens.Panel2Collapsed = true;
                        ((ScreenManagerUserInterface)UI).splitScreensPanel.Panel2Collapsed = true;

                        // TODO: First screen becomes the active screen...
                        break;
                    default:
                        break;
                }

                // Afficher les �crans.
                ICommand css = new CommandShowScreens(this);
                CommandManager.LaunchCommand(css);

                OrganizeMenus();
            }
            else
            {
                cm.UnstackLastCommand();
                m_bCancelLastCommand = false;
            }
        }
        private void mnuCloseFile2OnClick(object sender, EventArgs e)
        {
            //--------------------------------------------------------------------
            // Dans cet event Handler, on ferme toujours le second �cran.
            // Si on a pu cliquer, c'est qu'il y a forc�ment une vid�o de charg�e 
            // dans le second �cran.
            // Donc il y a deux �crans, mais le premier n'est pas forc�ment plein
            //--------------------------------------------------------------------
            // Supprimer explicitement l'�cran de la liste...
            IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
            CommandManager cm = CommandManager.Instance();
            cm.LaunchUndoableCommand(crs);

            if (!m_bCancelLastCommand)
            {

                // Supprimer l'�cran de la liste des contr�les du panel
                ((ScreenManagerUserInterface)UI).splitScreens.Panel2.Controls.Clear();

                // Ne garder que le premier de visible.
                ((ScreenManagerUserInterface)UI).splitScreens.Panel2Collapsed = true;
                ((ScreenManagerUserInterface)UI).splitScreensPanel.Panel2Collapsed = true;

                // Afficher les �crans.
                ICommand css = new CommandShowScreens(this);
                CommandManager.LaunchCommand(css);

                // TODO : The other screen becomes the active screen.

                OrganizeMenus();
            }
            else
            {
                cm.UnstackLastCommand();
                m_bCancelLastCommand = false;
            }

        }
        public void mnuSaveOnClick(object sender, EventArgs e)
        {
        	//---------------------------------------------------------------------------
            // Launch the dialog box where the user can choose to save the video,
            // the metadata or both.
            // Public because accessed from the closing command when we realize there are 
            // unsaved modified data.
            //---------------------------------------------------------------------------
            
            PlayerScreen ps = m_ActiveScreen as PlayerScreen;
            if (ps != null)
            {
            	DoStopPlaying();
                DoDeactivateKeyboardHandler();
            	
                ps.Save();
                
                DoActivateKeyboardHandler();
            }
        }
        private void mnuExportToPDFOnClick(object sender, EventArgs e)
        {
            /*if (m_ActiveScreen != null)
            {
                if (m_ActiveScreen is PlayerScreen)
                {
                    if (((PlayerScreen)m_ActiveScreen).m_PlayerScreenUI.Metadata.HasData)
                    {
                        DoStopPlaying();

                        SaveFileDialog saveFileDialog = new SaveFileDialog();
                        saveFileDialog.Title = m_resManager.GetString("dlgExportToPDF_Title", Thread.CurrentThread.CurrentUICulture);
                        saveFileDialog.RestoreDirectory = true;
                        saveFileDialog.Filter = m_resManager.GetString("dlgExportToPDF_Filter", Thread.CurrentThread.CurrentUICulture);
                        saveFileDialog.FilterIndex = 1;
                        saveFileDialog.FileName = Path.GetFileNameWithoutExtension(((PlayerScreen)m_ActiveScreen).m_PlayerScreenUI.Metadata.FullPath);

                        if (saveFileDialog.ShowDialog() == DialogResult.OK)
                        {
                            string filePath = saveFileDialog.FileName;
                            if (filePath.Length > 0)
                            {
                                AnalysisExporterPDF aepdf = new AnalysisExporterPDF();
                                aepdf.Export(filePath, ((PlayerScreen)m_ActiveScreen).m_PlayerScreenUI.Metadata);
                            }
                        }
                    }
                }
            }*/
        }
        private void mnuLoadAnalysisOnClick(object sender, EventArgs e)
        {
            if (m_ActiveScreen != null)
            {
                if (m_ActiveScreen is PlayerScreen)
                {
                    if (((PlayerScreen)m_ActiveScreen).FrameServer.Metadata.HasData)
                    {
                        // TODO : Merge mechanics.
                        LoadAnalysis();
                    }
                    else
                    {
                        LoadAnalysis();
                    }
                }
            }
        }
        private void LoadAnalysis()
        {
            DoStopPlaying();

            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = m_resManager.GetString("dlgLoadAnalysis_Title", Thread.CurrentThread.CurrentUICulture);
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Filter = m_resManager.GetString("dlgLoadAnalysis_Filter", Thread.CurrentThread.CurrentUICulture);
            openFileDialog.FilterIndex = 1;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;
                if (filePath.Length > 0)
                {
                    ((PlayerScreen)m_ActiveScreen).FrameServer.Metadata.LoadFromFile(filePath);
                    ((PlayerScreen)m_ActiveScreen).m_PlayerScreenUI.PostImportMetadata();
                }
            }
        }
        private void mnuExportODF_OnClick(object sender, EventArgs e)
        {
        	ExportSpreadsheet(MetadataExportFormat.ODF);
        }
        private void mnuExportMSXML_OnClick(object sender, EventArgs e)
        {
        	ExportSpreadsheet(MetadataExportFormat.MSXML);	
        }
        private void mnuExportXHTML_OnClick(object sender, EventArgs e)
        {
        	ExportSpreadsheet(MetadataExportFormat.XHTML);
        }
        private void ExportSpreadsheet(MetadataExportFormat _format)
        {
        	PlayerScreen player = m_ActiveScreen as PlayerScreen;
        	if (player != null)
            {
	            if (player.FrameServer.Metadata.HasData)
                {
                    DoStopPlaying();    

	            	SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.Title = m_resManager.GetString("dlgExportSpreadsheet_Title", Thread.CurrentThread.CurrentUICulture);
                    saveFileDialog.RestoreDirectory = true;
                    saveFileDialog.Filter = m_resManager.GetString("dlgExportSpreadsheet_Filter", Thread.CurrentThread.CurrentUICulture);
                    
                    saveFileDialog.FilterIndex = ((int)_format) + 1;
                        
                    saveFileDialog.FileName = Path.GetFileNameWithoutExtension(player.FrameServer.Metadata.FullPath);

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        string filePath = saveFileDialog.FileName;
                        if (filePath.Length > 0)
                        {
                        	player.FrameServer.Metadata.Export(filePath, _format);  
                        }
                    }
	            }
        	}
        }
        #endregion

        #region View
        private void mnuOnePlayerOnClick(object sender, EventArgs e)
        {
        	//------------------------------------------------------------
        	// - Reorganize the list so it conforms to the asked combination.
        	// - Display the new list.
        	// 
        	// Here : One player screen.
        	//------------------------------------------------------------
            
            m_bSynching = false;
            CommandManager cm = CommandManager.Instance();

            switch (screenList.Count)
            {
                case 0:
            		{
	                    // Currently : 0 screens. -> add a player.
	                    IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
	                    cm.LaunchUndoableCommand(caps);
	                    break;
            		}
                case 1:
            		{
	            		if(screenList[0] is CaptureScreen)
	                    {
	                    	// Currently : 1 capture. -> remove and add a player.
	                    	IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
	                    	cm.LaunchUndoableCommand(caps);
	                    }
	                    else
	                    {
	                    	// Currently : 1 player. -> do nothing.
	                    }
	                    break;
            		}
                case 2:
            		{
	                    // We need to decide which screen(s) to remove.
						// Possible cases :
						// [capture][capture] -> remove both and add player.
						// [capture][player] -> remove capture.
						// [player][capture] -> remove capture.	
						// [player][player] -> depends on emptiness.
						
						if(screenList[0] is CaptureScreen && screenList[1] is CaptureScreen)
						{
							// [capture][capture] -> remove both and add player.
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand crs2 = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs2);
							IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
	                    	cm.LaunchUndoableCommand(caps);
						}
						else if(screenList[0] is CaptureScreen && screenList[1] is PlayerScreen)
						{
							// [capture][player] -> remove capture.	
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
	                    	cm.LaunchUndoableCommand(caps);
						}
						else if(screenList[0] is PlayerScreen && screenList[1] is CaptureScreen)
						{
							// [player][capture] -> remove capture.	
							IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
	                    	cm.LaunchUndoableCommand(caps);
						}
						else
						{
							//---------------------------------------------
							// [player][player] -> depends on emptiness :
							// 
							// [empty][full] -> remove empty. 
							// [full][full] -> remove second one (right).
							// [full][empty] -> remove empty (right).
							// [empty][empty] -> remove second one (right).
							//---------------------------------------------
							
							if(!screenList[0].Full && screenList[1].Full)
							{
								// remove [0].
								IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
								cm.LaunchUndoableCommand(crs);
	
								// todo : document me.
	                            if (!m_bCancelLastCommand)
	                            {
	                                // Hmmm. This should be under commandshowscreen responsibility ?
	                                ((ScreenManagerUserInterface)UI).splitScreens.Panel1.Controls.Add(((PlayerScreen)screenList[0]).m_PlayerScreenUI);
	                            }
	                            else
	                            {
	                                cm.UnstackLastCommand();
	                                m_bCancelLastCommand = false;
	                            }
							}
							else
							{
								// remove [1].
								IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
								cm.LaunchUndoableCommand(crs);
	
								// todo: doc.
	                            if(m_bCancelLastCommand)
	                            {
	                                cm.UnstackLastCommand();
	                                m_bCancelLastCommand = false;
	                            }	
							}
						}
	                    break;
            		}
                default:
                    break;
            }

            // Display the new list.
            ICommand css = new CommandShowScreens(this);
            CommandManager.LaunchCommand(css);
            
            // Mettre � jour les menus
            OrganizeMenus();
        }
        private void mnuTwoPlayersOnClick(object sender, EventArgs e)
        {
            //------------------------------------------------------------
        	// - Reorganize the list so it conforms to the asked combination.
        	// - Display the new list.
        	// 
        	// Here : Two player screens.
        	//------------------------------------------------------------
            m_bSynching = false;
            CommandManager cm = CommandManager.Instance();
            
            switch (screenList.Count)
            {
                case 0:
                    {
            			// Currently : 0 screens. -> add two players.
                        // We use two different commands to keep the undo history working.
            			IUndoableCommand caps1 = new CommandAddPlayerScreen(this, true);
                        cm.LaunchUndoableCommand(caps1);
                        IUndoableCommand caps2 = new CommandAddPlayerScreen(this, true);
                        cm.LaunchUndoableCommand(caps2);
                        break;
                    }
                case 1:
                    {
            			if(screenList[0] is CaptureScreen)
	                    {
	                    	// Currently : 1 capture. -> remove and add 2 players.
	                    	IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand caps1 = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps1);
                       		IUndoableCommand caps2 = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps2);
	                    }
	                    else
	                    {
	                    	// Currently : 1 player. -> add another.
	                    	IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps);
	                    }                    
                        break;
                    }
                case 2:
            		{
            			// We need to decide which screen(s) to remove.
						// Possible cases :
						// [capture][capture] -> remove both and add two players.
						// [capture][player] -> remove capture and add player.
						// [player][capture] -> remove capture and add player.	
						// [player][player] -> do nothing.
						
            			if(screenList[0] is CaptureScreen && screenList[1] is CaptureScreen)
						{
							// [capture][capture] -> remove both and add two players.
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand crs2 = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs2);
							IUndoableCommand caps1 = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps1);
                       		IUndoableCommand caps2 = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps2);
						}
						else if(screenList[0] is CaptureScreen && screenList[1] is PlayerScreen)
						{
							// [capture][player] -> remove capture and add player.
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps);
						}
						else if(screenList[0] is PlayerScreen && screenList[1] is CaptureScreen)
						{
							// [player][capture] -> remove capture and add player.
							IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps);
						}
						else
						{
            				// [player][player] -> do nothing.
						}
						
                    	break;
            		}
                default:
                    break;
            }

            // Display the new list.
            ICommand css = new CommandShowScreens(this);
            CommandManager.LaunchCommand(css);
            
            OrganizeMenus();
        }
        private void mnuOneCaptureOnClick(object sender, EventArgs e)
        {
            //------------------------------------------------------------
        	// - Reorganize the list so it conforms to the asked combination.
        	// - Display the new list.
        	// 
        	// Here : One capture screens.
        	//------------------------------------------------------------
            m_bSynching = false;
            CommandManager cm = CommandManager.Instance();
           
           	switch (screenList.Count)
            {
                case 0:
           			{
	                    // Currently : 0 screens. -> add a capture.
	                    IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
	                    cm.LaunchUndoableCommand(cacs);
	                    break;
           			}
                case 1:
                    {
	                    if(screenList[0] is PlayerScreen)
	                    {
	                    	// Currently : 1 player. -> remove and add a capture.
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
	                    	cm.LaunchUndoableCommand(cacs);
	
							// todo : m_bCancelLastCommand ?
	                    }
	                    else
	                    {
	                    	// Currently : 1 capture. -> do nothing.
	                    }
	                    break;
                    }
                case 2:
           			{
	                    // We need to decide which screen(s) to remove.
						// Possible cases :
						// [capture][capture] -> depends on emptiness.
						// [capture][player] -> remove player.
						// [player][capture] -> remove player.	
						// [player][player] -> remove both and add capture.
						
						if(screenList[0] is CaptureScreen && screenList[1] is CaptureScreen)
						{
							//---------------------------------------------
							// [capture][capture] -> depends on emptiness.
							// 
							// [empty][full] -> remove empty.
							// [full][full] -> remove second one (right).
							// [full][empty] -> remove empty (right).
							// [empty][empty] -> remove second one (right).
							//---------------------------------------------
							
							if(!screenList[0].Full && screenList[1].Full)
							{
								// remove [0].
								IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
								cm.LaunchUndoableCommand(crs);
	
								// todo : m_bCancelLastCommand ?
							}
							else
							{
								// remove [1].
								IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
								cm.LaunchUndoableCommand(crs);
	
								// todo: doc.
	                            if(m_bCancelLastCommand)
	                            {
	                                cm.UnstackLastCommand();
	                                m_bCancelLastCommand = false;
	                            }	
							}
						}
						else if(screenList[0] is CaptureScreen && screenList[1] is PlayerScreen)
						{
							// [capture][player] -> remove player.	
							IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs);
						}
						else if(screenList[0] is PlayerScreen && screenList[1] is CaptureScreen)
						{
							// [player][capture] -> remove player.
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
						}
						else
						{
							// remove both and add one capture.
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand crs2 = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs2);
							IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
	                    	cm.LaunchUndoableCommand(cacs);
						}
	                    break;
           			}
                default:
                    break;
            }

            // Display the new list.
            ICommand css = new CommandShowScreens(this);
            CommandManager.LaunchCommand(css);
            
            OrganizeMenus();
        }
        private void mnuTwoCapturesOnClick(object sender, EventArgs e)
        {
            //------------------------------------------------------------
        	// - Reorganize the list so it conforms to the asked combination.
        	// - Display the new list.
        	// 
        	// Here : Two capture screens.
        	//------------------------------------------------------------
            m_bSynching = false;
            CommandManager cm = CommandManager.Instance();
            
            switch (screenList.Count)
            {
                case 0:
                    {
            			// Currently : 0 screens. -> add two capture.
                        // We use two different commands to keep the undo history working.
            			IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
                        cm.LaunchUndoableCommand(cacs);
                        IUndoableCommand cacs2 = new CommandAddCaptureScreen(this, true);
                        cm.LaunchUndoableCommand(cacs2);
                        break;
                    }
                case 1:
                    {
            			if(screenList[0] is CaptureScreen)
	                    {
	                    	// Currently : 1 capture. -> add another.
	                    	IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
                        	cm.LaunchUndoableCommand(cacs);
	                    }
	                    else
	                    {
	                    	// Currently : 1 player. -> remove and add 2 capture.
	                    	IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
	                    	IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
                        	cm.LaunchUndoableCommand(cacs);
                        	IUndoableCommand cacs2 = new CommandAddCaptureScreen(this, true);
                        	cm.LaunchUndoableCommand(cacs2);
	                    }                   
                        break;
                    }
                case 2:
            		{
            			// We need to decide which screen(s) to remove.
						// Possible cases :
						// [capture][capture] -> do nothing.
						// [capture][player] -> remove player and add capture.
						// [player][capture] -> remove player and add capture.	
						// [player][player] -> remove both and add 2 capture.
						
            			if(screenList[0] is CaptureScreen && screenList[1] is CaptureScreen)
						{
							// [capture][capture] -> do nothing.
						}
						else if(screenList[0] is CaptureScreen && screenList[1] is PlayerScreen)
						{
							// [capture][player] -> remove player and add capture.
							IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
                        	cm.LaunchUndoableCommand(cacs);
						}
						else if(screenList[0] is PlayerScreen && screenList[1] is CaptureScreen)
						{
							// [player][capture] -> remove player and add capture.
							IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
                        	cm.LaunchUndoableCommand(cacs);
						}
						else
						{
            				// [player][player] -> remove both and add 2 capture.
            				IUndoableCommand crs = new CommandRemoveScreen(this, 0, true);
							cm.LaunchUndoableCommand(crs);
            				IUndoableCommand crs2 = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs2);
							IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
                        	cm.LaunchUndoableCommand(cacs);
                        	IUndoableCommand cacs2 = new CommandAddCaptureScreen(this, true);
                        	cm.LaunchUndoableCommand(cacs2);
						}
						
                    	break;
            		}
                default:
                    break;
            }
            
            // Display the new list.
            ICommand css = new CommandShowScreens(this);
            CommandManager.LaunchCommand(css);
            
            OrganizeMenus();
        }
        private void mnuTwoMixedOnClick(object sender, EventArgs e)
        {
            //------------------------------------------------------------
        	// - Reorganize the list so it conforms to the asked combination.
        	// - Display the new list.
        	// 
        	// Here : Mixed screen. The workspace preset is : [capture][player]
        	//------------------------------------------------------------
            m_bSynching = false;
            CommandManager cm = CommandManager.Instance();
            
            switch (screenList.Count)
            {
                case 0:
                    {
            			// Currently : 0 screens. -> add a capture and a player.
                        IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
	                    cm.LaunchUndoableCommand(cacs);
            			IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
                        cm.LaunchUndoableCommand(caps);
                        break;
                    }
                case 1:
                    {
            			if(screenList[0] is CaptureScreen)
	                    {
	                    	// Currently : 1 capture. -> add a player.
	                    	IUndoableCommand caps = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps);
	                    }
	                    else
	                    {
	                    	// Currently : 1 player. -> add a capture.
	                    	IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
	                    	cm.LaunchUndoableCommand(cacs);
	                    }                    
                        break;
                    }
                case 2:
            		{
            			// We need to decide which screen(s) to remove/replace.
						
            			if(screenList[0] is CaptureScreen && screenList[1] is CaptureScreen)
						{
							// [capture][capture] -> remove right and add player.
							IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand caps1 = new CommandAddPlayerScreen(this, true);
                        	cm.LaunchUndoableCommand(caps1);
						}
						else if(screenList[0] is CaptureScreen && screenList[1] is PlayerScreen)
						{
							// [capture][player] -> do nothing.
						}
						else if(screenList[0] is PlayerScreen && screenList[1] is CaptureScreen)
						{
							// [player][capture] -> do nothing.
						}
						else
						{
            				// [player][player] -> remove right and add capture.
            				IUndoableCommand crs = new CommandRemoveScreen(this, 1, true);
							cm.LaunchUndoableCommand(crs);
							IUndoableCommand cacs = new CommandAddCaptureScreen(this, true);
	                    	cm.LaunchUndoableCommand(cacs);
						}
						
                    	break;
            		}
                default:
                    break;
            }

            // Display the new list.
            ICommand css = new CommandShowScreens(this);
            CommandManager.LaunchCommand(css);
            
            OrganizeMenus();
        }
        private void mnuSwapScreensOnClick(object sender, EventArgs e)
        {
            if (screenList.Count == 2)
            {
                IUndoableCommand command = new CommandSwapScreens(this);
                CommandManager cm = CommandManager.Instance();
                cm.LaunchUndoableCommand(command);
            }
        }
        private void mnuToggleCommonCtrlsOnClick(object sender, EventArgs e)
        {
            IUndoableCommand ctcc = new CommandToggleCommonControls(((ScreenManagerUserInterface)UI).splitScreensPanel);
            CommandManager cm = CommandManager.Instance();
            cm.LaunchUndoableCommand(ctcc);
            
            m_bCommonControlsVisible = !((ScreenManagerUserInterface)UI).splitScreensPanel.Panel2Collapsed;
        }
        #endregion

        #region Image
        private void mnuDeinterlaceOnClick(object sender, EventArgs e)
        {
        	PlayerScreen player = m_ActiveScreen as PlayerScreen;
        	if(player != null)
        	{
        		mnuDeinterlace.Checked = !mnuDeinterlace.Checked;
        		player.Deinterlaced = mnuDeinterlace.Checked;	
        	}
        }
        private void mnuFormatAutoOnClick(object sender, EventArgs e)
        {
        	PlayerScreen player = m_ActiveScreen as PlayerScreen;
        	if(player != null)
        	{
        		if(player.AspectRatio != VideoFiles.AspectRatio.AutoDetect)
        		{
	        		mnuFormatForce43.Checked = false;
	        		mnuFormatForce169.Checked = false;
	        		mnuFormatAuto.Checked = true;
	        		player.AspectRatio = VideoFiles.AspectRatio.AutoDetect;	
        		}
        	}
        }
        private void mnuFormatForce43OnClick(object sender, EventArgs e)
        {
        	PlayerScreen player = m_ActiveScreen as PlayerScreen;
        	if(player != null)
        	{
        		if(player.AspectRatio != VideoFiles.AspectRatio.Force43)
        		{
	        		mnuFormatForce43.Checked = true;
	        		mnuFormatForce169.Checked = false;
	        		mnuFormatAuto.Checked = false;
	        		player.AspectRatio = VideoFiles.AspectRatio.Force43;
        		}
        	}
        }
        private void mnuFormatForce169OnClick(object sender, EventArgs e)
        {
        	PlayerScreen player = m_ActiveScreen as PlayerScreen;
        	if(player != null)
        	{
        		if(player.AspectRatio != VideoFiles.AspectRatio.Force169)
        		{
	        		mnuFormatForce43.Checked = false;
	        		mnuFormatForce169.Checked = true;
	        		mnuFormatAuto.Checked = false;
	        		player.AspectRatio = VideoFiles.AspectRatio.Force169;
        		}
        	}
        }        
        private void mnuMirrorOnClick(object sender, EventArgs e)
        {
        	PlayerScreen player = m_ActiveScreen as PlayerScreen;
        	if(player != null)
        	{
        		mnuMirror.Checked = !mnuMirror.Checked;
        		player.Mirrored = mnuMirror.Checked;
        	}
        }
        private void mnuGridOnClick(object sender, EventArgs e)
        {
        	if (m_ActiveScreen != null && m_ActiveScreen.CapabilityDrawings)
        	{
        		mnuGrid.Checked = !mnuGrid.Checked;
        		if(m_ActiveScreen is PlayerScreen)
        		{
        			((PlayerScreen)m_ActiveScreen).ShowGrid = mnuGrid.Checked;
        		}
        		else if(m_ActiveScreen is CaptureScreen)
        		{
        			((CaptureScreen)m_ActiveScreen).ShowGrid = mnuGrid.Checked;
        		}
        	}
        }
        private void mnu3DPlaneOnClick(object sender, EventArgs e)
        {
        	if (m_ActiveScreen != null && m_ActiveScreen.CapabilityDrawings)
        	{
        		mnu3DPlane.Checked = !mnu3DPlane.Checked;
        		if(m_ActiveScreen is PlayerScreen)
        		{
        			((PlayerScreen)m_ActiveScreen).Show3DPlane = mnu3DPlane.Checked;
        		}
        		else if(m_ActiveScreen is CaptureScreen)
        		{
        			((CaptureScreen)m_ActiveScreen).Show3DPlane = mnu3DPlane.Checked;
        		}
        	}
        }
        #endregion

        #endregion

        #region D�l�gu�es appell�es depuis l'UI
        public void DropLoadMovie(string _FilePath, int _iScreen)
        {
            // End of drag and drop between FileManager and ScreenManager
            DoLoadMovieInScreen(_FilePath, _iScreen, true);
        }
        public void CommonCtrlsGotoFirst(object sender, EventArgs e)
        {
            
        }
        public void CommonCtrlsGotoLast(object sender, EventArgs e)
        {
           
        }
        public void CommonCtrlsGotoPrev(object sender, EventArgs e)
        {
            
        }
        public void CommonCtrlsGotoNext(object sender, EventArgs e)
        {
            
        }
        public void CommonCtrlsPlay(object sender, EventArgs e)
        {
            
        }
        public void CommonCtrlsSwap(object sender, EventArgs e)
        {
            
        }
        public void CommonCtrlsSync(object sender, EventArgs e)
        {
            
        }
        public void CommonCtrlsPositionChanged(object sender, long _iPosition)
        {
            
        }
        #endregion

        #region Delegates called from anywhere, through Services
        public void DoLoadMovieInScreen(string _filePath, int _iForceScreen, bool _bStoreState)
        {
        	if(File.Exists(_filePath))
            {
            	IUndoableCommand clmis = new CommandLoadMovieInScreen(this, _filePath, _iForceScreen, _bStoreState);
            	CommandManager cm = CommandManager.Instance();
            	cm.LaunchUndoableCommand(clmis);
            	
            	// No need to call PrepareSync here because it will be called when the working zone is set anyway.
        	}
        }
        public void DoStopPlaying()
        {
            // Called from Supervisor, when user launch open dialog box.
            
            // 1. Stop each screen.
            foreach (AbstractScreen screen in screenList)
            {
                if (screen is PlayerScreen)
                {
                    ((PlayerScreen)screen).StopPlaying();
                }
            }

            // 2. Stop the common timer.
            StopDynamicSync();
            ((ScreenManagerUserInterface)UI).DisplayAsPaused();
        }
        public void DoDeactivateKeyboardHandler()
        {
            m_bAllowKeyboardHandler = false;
        }
        public void DoActivateKeyboardHandler()
        {
            m_bAllowKeyboardHandler = true;
        }
        public void DoVideoProcessingDone(DrawtimeFilterOutput _dfo)
        {
        	// Todo, disable draw time filter in player.
        	if(_dfo != null)
        	{
    			m_VideoFilters[_dfo.VideoFilterType].Menu.Checked = _dfo.Active;
    			
        		PlayerScreen player = m_ActiveScreen as PlayerScreen;
	        	if(player != null)
	        	{
	        		player.SetDrawingtimeFilterOutput(_dfo);
	        	}
        	}
        	
        	m_ActiveScreen.RefreshImage();
        }
        #endregion

        #region Keyboard Handling
        private bool OnKeyPress(Keys _keycode)
        {
        	//---------------------------------------------------------
        	// Here are grouped the handling of the keystrokes that are 
        	// screen manager's responsibility.
        	// And only when the common controls are actually visible.
        	//---------------------------------------------------------
        	bool bWasHandled = false;
        	ScreenManagerUserInterface smui = UI as ScreenManagerUserInterface;
            	
			if (smui != null)
            {
	        	switch (_keycode)
				{
	        		case Keys.Space:
	        		case Keys.Return:
	        			{
	                       	smui.ComCtrls.buttonPlay_Click(null, EventArgs.Empty);
	                        bWasHandled = true;
	                    	break;
	        			}
	        		case Keys.Left:
	        			{
							smui.ComCtrls.buttonGotoPrevious_Click(null, EventArgs.Empty);
                        	bWasHandled = true;
	        				break;
	        			}
	        		case Keys.Right:
	        			{
                           	smui.ComCtrls.buttonGotoNext_Click(null, EventArgs.Empty);
                       		bWasHandled = true;
							break;
	        			}
	        		case Keys.End:
                        {
	        				smui.ComCtrls.buttonGotoLast_Click(null, EventArgs.Empty);
	        				bWasHandled = true;
							break;
	        			}
	        		case Keys.Home:
	        			{
	        				smui.ComCtrls.buttonGotoFirst_Click(null, EventArgs.Empty);
                            bWasHandled = true;
                            break;
	        			}
	        		default:
	        			break;
	        	}
			}
        	return bWasHandled;
        }
        private void ActivateOtherScreen()
        {
        	if (screenList.Count == 2)
            {
                if (m_ActiveScreen == screenList[0])
                {
                    Screen_SetActiveScreen(screenList[1]);
                }
                else
                {
                    Screen_SetActiveScreen(screenList[0]);
                }
            }	
        }
        #endregion

        #region Synchronisation
        private void PrepareSync(bool _bInitialization)
        {
        	// Called each time the screen list change 
        	// or when a screen changed selection.
        	
        	// We don't care which video was updated.
            // Set sync mode and reset sync.
            m_bSynching = false;

            if ( (screenList.Count == 2))
            {
                if ((screenList[0] is PlayerScreen) && (screenList[1] is PlayerScreen))
                {
                    if (((PlayerScreen)screenList[0]).Full && ((PlayerScreen)screenList[1]).Full)
                    {
                        m_bSynching = true;
                        ((PlayerScreen)screenList[0]).Synched = true;
                        ((PlayerScreen)screenList[1]).Synched = true;

                        if (_bInitialization)
                        {
                        	log.Debug("PrepareSync() - Initialization (reset of sync point).");
                            // Static Sync
                            m_iRightSyncFrame = 0;
                            m_iLeftSyncFrame = 0;
                            m_iSyncLag = 0;
                            m_iCurrentFrame = 0;
                            
                            ((PlayerScreen)screenList[0]).SyncPosition = 0;
	                		((PlayerScreen)screenList[1]).SyncPosition = 0;
	                		((ScreenManagerUserInterface)UI).UpdateSyncPosition(m_iCurrentFrame);

                            // Dynamic Sync
                            ResetDynamicSyncFlags();
                            
                            // Sync Merging
                            ((PlayerScreen)screenList[0]).SyncMerge = false;
	                		((PlayerScreen)screenList[1]).SyncMerge = false;
	                		((ScreenManagerUserInterface)UI).ComCtrls.SyncMerging = false;
                        }

                        // Mise � jour trkFrame
                        SetSyncLimits();
                        ((ScreenManagerUserInterface)UI).SetupTrkFrame(0, m_iMaxFrame, m_iCurrentFrame);

                        // Mise � jour Players
                        OnCommonPositionChanged(m_iCurrentFrame);

                        // debug
                        ((ScreenManagerUserInterface)UI).DisplaySyncLag(m_iSyncLag);
                    }
                    else
                    {
                        // Not all screens are loaded with videos.
                        ((PlayerScreen)screenList[0]).Synched = false;
                        ((PlayerScreen)screenList[1]).Synched = false;
                    }
                }
            }
            else
            {
                // Only one screen, or not all screens are PlayerScreens.
                switch (screenList.Count)
                {
                    case 1:
                        if (screenList[0] is PlayerScreen)
                        {
                            ((PlayerScreen)screenList[0]).Synched = false;
                        }
                        break;
                    case 2:
                        if (screenList[0] is PlayerScreen)
                        {
                            ((PlayerScreen)screenList[0]).Synched = false;
                        }
                        if (screenList[1] is PlayerScreen)
                        {
                            ((PlayerScreen)screenList[1]).Synched = false;
                        }
                        break;
                    default:
                        break;
                }
            }

            if (!m_bSynching) 
            { 
                StopDynamicSync();
                ((ScreenManagerUserInterface)UI).DisplayAsPaused();
            }
        }
        public void SetSyncPoint(bool _bIntervalOnly)
        {
            //--------------------------------------------------------------------------------------------------
            // Registers the current position of each video as its sync frame. (Optional)
            // Computes the lag in common timestamps between positions.
            // Computes the lag in milliseconds between positions. (using current framerate of each video)
            // Update current common position.
            // (public only because accessed from the Swap command.)
            //--------------------------------------------------------------------------------------------------

            //---------------------------------------------------------------------------
            // Par d�faut les deux vid�os sont synchronis�es sur {0}.
            // Le param�tre de synchro se lit comme suit : 
            // {+2} : La vid�o de droite � 2 frames d'avance sur celle de gauche.
            // {-4} : La vid�o de droite � 4 frames de retard.
            //
            // Si le d�calage est positif, la vid�o de droite doit partir en premier.
            // La pause de terminaison d�pend � la fois du param�tre de synchro et 
            // des dur�es (en frames) respectives des deux vid�os.
            //
            // Si _bIntervalOnly == true, on ne veut pas changer les frames de r�f�rence
            // (G�n�ralement apr�s une modification du framerate de l'une des vid�os ou swap)
            //----------------------------------------------------------------------------
            if (m_bSynching && screenList.Count == 2)
            {
	            // Registers current positions.
	            if (!_bIntervalOnly)
	            {
	                // For timing label only
	                ((PlayerScreen)screenList[0]).SyncPosition = ((PlayerScreen)screenList[0]).Position;
	                ((PlayerScreen)screenList[1]).SyncPosition = ((PlayerScreen)screenList[1]).Position;
	
	                m_iLeftSyncFrame = ((PlayerScreen)screenList[0]).CurrentFrame;
	                m_iRightSyncFrame = ((PlayerScreen)screenList[1]).CurrentFrame;
	                
	                log.Debug(String.Format("New Sync Points:[{0}][{1}], Sync lag:{2}",m_iLeftSyncFrame, m_iRightSyncFrame, m_iRightSyncFrame - m_iLeftSyncFrame));
	            }
	
	
	            // Sync Lag is expressed in frames.
	            m_iSyncLag = m_iRightSyncFrame - m_iLeftSyncFrame;
	
	            // We need to recompute the lag in milliseconds because it can change even when 
	            // the references positions don't change. For exemple when varying framerate (speed).
	            int iLeftSyncMilliseconds = (int)(((PlayerScreen)screenList[0]).FrameInterval * m_iLeftSyncFrame);
	            int iRightSyncMilliseconds = (int)(((PlayerScreen)screenList[1]).FrameInterval * m_iRightSyncFrame);
	            m_iSyncLagMilliseconds = iRightSyncMilliseconds - iLeftSyncMilliseconds;
	
	            // Update common position (sign of m_iSyncLag might have changed.)
	            if (m_iSyncLag > 0)
	            {
	                m_iCurrentFrame = m_iRightSyncFrame;
	            }
	            else
	            {
	                m_iCurrentFrame = m_iLeftSyncFrame;
	            }
	
	            ((ScreenManagerUserInterface)UI).UpdateSyncPosition(m_iCurrentFrame);
	            ((ScreenManagerUserInterface)UI).DisplaySyncLag(m_iSyncLag);
            }
        }
        private void SetSyncLimits()
        {
            //--------------------------------------------------------------------------------
            // Computes the real max of the trkFrame, considering the lag and original sizes.
            // Updates trkFrame bounds, expressed in *Frames*.
            // impact : m_iMaxFrame.
            //---------------------------------------------------------------------------------
			log.Debug("SetSyncLimits() called.");
            int iLeftMaxFrame = ((PlayerScreen)screenList[0]).LastFrame;
            int iRightMaxFrame = ((PlayerScreen)screenList[1]).LastFrame;

            if (m_iSyncLag > 0)
            {
                // Lag is positive. Right video starts first and its duration stay the same as original.
                // Left video has to wait for an ammount of time.

                // Get Lag in number of frames of left video.
                //int iSyncLagFrames = ((PlayerScreen)screenList[0]).NormalizedToFrame(m_iSyncLag);

                // Check if lag is still valid. (?)
                if (m_iSyncLag > iRightMaxFrame) 
                {
                    m_iSyncLag = 0; 
                }

                iLeftMaxFrame += m_iSyncLag;
            }
            else
            {
                // Lag is negative. Left video starts first and its duration stay the same as original.
                // Right video has to wait for an ammount of time.
                
                // Get Lag in frames of right video
                //int iSyncLagFrames = ((PlayerScreen)screenList[1]).NormalizedToFrame(m_iSyncLag);

                // Check if lag is still valid.(?)
                if (-m_iSyncLag > iLeftMaxFrame) { m_iSyncLag = 0; }
                iRightMaxFrame += (-m_iSyncLag);
            }

            m_iMaxFrame = Math.Max(iLeftMaxFrame, iRightMaxFrame);

            //Console.WriteLine("m_iSyncLag:{0}, m_iSyncLagMilliseconds:{1}, MaxFrames:{2}", m_iSyncLag, m_iSyncLagMilliseconds, m_iMaxFrame);
        }
        private void OnCommonPositionChanged(int _iFrame)
        {
            //------------------------------------------------------------------------------
            // This is where the "static sync" is done.
            // Updates each video to reflect current common position.
            // Used to handle GotoNext, GotoPrev, trkFrame, etc.
            // 
            // note: m_iSyncLag and _iFrame are expressed in frames.
            //------------------------------------------------------------------------------

            //log.Debug(String.Format("Static Sync, common position changed to {0}",_iFrame));
            
            // Get corresponding position in each video, in frames
            int iLeftFrame = 0;
            int iRightFrame = 0;

            if (_iFrame >= 0)
            {
                if (m_iSyncLag > 0)
                {
                    // Right video must go ahead.

                    iRightFrame = _iFrame;
                    iLeftFrame = _iFrame - m_iSyncLag;
                    if (iLeftFrame < 0)
                    {
                        iLeftFrame = 0;
                    }
                }
                else
                {
                    // Left video must go ahead.

                    iLeftFrame = _iFrame;
                    iRightFrame = _iFrame - (-m_iSyncLag);
                    if (iRightFrame < 0)
                    {
                        iRightFrame = 0;
                    }
                }

                // Force positions.
                ((PlayerScreen)screenList[0]).CurrentFrame = iLeftFrame;
                ((PlayerScreen)screenList[1]).CurrentFrame = iRightFrame;
            }
            else
            {
                // Special case for ++.
                if (m_iSyncLag > 0)
                {
                    // Right video must go ahead.
                    ((PlayerScreen)screenList[1]).GotoNextFrame();

                    if (m_iCurrentFrame > m_iSyncLag)
                    {
                        ((PlayerScreen)screenList[0]).GotoNextFrame();
                    }
                }
                else
                {
                    // Left video must go ahead.
                    ((PlayerScreen)screenList[0]).GotoNextFrame();

                    if (m_iCurrentFrame > -m_iSyncLag)
                    {
                        ((PlayerScreen)screenList[1]).GotoNextFrame();
                    }
                }
            }
        }
        public void SwapSync()
        {
        	if (m_bSynching && screenList.Count == 2)
        	{
	        	int iTemp = m_iLeftSyncFrame;
	            m_iLeftSyncFrame = m_iRightSyncFrame;
	            m_iRightSyncFrame = iTemp;
	
	            // Reset dynamic sync flags
	            ResetDynamicSyncFlags();
        	}
        }
        private void StartDynamicSync(int _interval)
        {
        	m_bDynamicSynching = true;
        	DynamicSync();
        	/*
            if (m_DelegateMMTimerEventHandler != null)
            {
                int myData = 0;	// dummy data
                m_IdMultimediaTimer = timeSetEvent( _interval,                              // D�lai en ms.
                                                    _interval,                              // Resolution en ms.
                                                    m_DelegateMMTimerEventHandler,          // event handler du tick.
                                                    ref myData,                             // ?
                                                    TIME_PERIODIC | TIME_KILL_SYNCHRONOUS); // Type d'event.
                log.Debug("Common multimedia timer started");
            }*/
        }
        private void StopDynamicSync()
        {
        	m_bDynamicSynching = false;
        	/*
            if (m_IdMultimediaTimer != 0)
            {
                timeKillEvent(m_IdMultimediaTimer);
                log.Debug("Common multimedia timer stopped");
            }*/
        }
        private void MultimediaTimer_Tick(uint id, uint msg, ref int userCtx, int rsv1, int rsv2)
        {
        	DynamicSync();
        }
        private void DynamicSync()
        {
        	// This is where the dynamic sync is done.
            // Get each video positions in common timebase and milliseconds.
            // Figure if a restart or pause is needed, considering current positions.
            
            // When the user press the common play button, we just propagate the play to the screens.
            // The common timer is just set to try to be notified of each frame change.
            // It is not used to provoke frame change itself.
            // We just start and stop the players timers when we detect one of the video has reached the end,
            // to prevent it from auto restarting.

            //-----------------------------------------------------------------------------
            // /!\ Following paragraph is obsolete when using Direct call to dynamic sync.
            // This function is executed in the WORKER THREAD.
            // nothing called from here should ultimately call in the UI thread.
            //
            // Except when using BeginInvoke.
            // But we can't use BeginInvoke here, because it's only available for Controls.
            // Calling the BeginInvoke of the PlayerScreenUI is useless because it's not the same 
            // UI thread as the one used to create the menus that we will update upon SetAsActiveScreen
            // 
            //-----------------------------------------------------------------------------

            // Glossary:
            // XIsStarting 		: currently on [0] but a Play was asked.
            // XIsCatchingUp 	: video is between [0] and the point where both video will be running. 
            
            
            if (m_bSynching && screenList.Count == 2)
            {
                // Function called by timer event handler, asynchronously on each tick.

                // L'ensemble de la supervision est r�alis�e en TimeStamps.
                // Seul les d�cision de lancer / arr�ter sont �tablies par rapport
                // au temps auquel on est.

                int iLeftPosition = ((PlayerScreen)screenList[0]).CurrentFrame;
                int iRightPosition = ((PlayerScreen)screenList[1]).CurrentFrame;
                int iLeftMilliseconds = (int)(iLeftPosition * ((PlayerScreen)screenList[0]).FrameInterval);
                int iRightMilliseconds = (int)(iRightPosition * ((PlayerScreen)screenList[1]).FrameInterval);

                //-----------------------------------------------------------------------
                // Dans cette fonction, on part du principe que les deux vid�os tournent.
                // Et on fait des 'Ensure Pause' quand n�cessaire.
                // On �vite les Ensure Play' car l'utilisateur a pu 
                // manuellement pauser une vid�o.
                //-----------------------------------------------------------------------
                #region [i][0]
                if (iLeftPosition > 0 && iRightPosition == 0)
                {
                	EnsurePlay(0);
                	
                    // Etat 4. [i][0]
                    m_bLeftIsStarting = false;

                    if (m_iSyncLag == 0)
                    {
                        //-----------------------------------------------------
                        // La vid�o de droite 
                        // - vient de boucler et on doit attendre l'autre
                        // - est en train de repartir.
                        //-----------------------------------------------------
                        if (!m_bRightIsStarting)
                        {
                            // Stop pour bouclage
                            EnsurePause(1);
                        }

                        m_iCurrentFrame = iLeftPosition;
                    }
                    else if (m_iSyncLagMilliseconds > 0)
                    {
                        // La vid�o de droite est sur 0 et doit partir en premier.
                        // Si elle n'est pas en train de repartir, c'est qu'on 
                        // doit attendre que la vid�o de gauche ait finit son tour.
                        if (!m_bRightIsStarting)
                        {
                            EnsurePause(1);
                            m_iCurrentFrame = iLeftPosition + m_iSyncLag;
                        }
                        else
                        {
                            m_iCurrentFrame = iLeftPosition;
                        }
                    }
                    else if (m_iSyncLagMilliseconds < 0)
                    {
                        // La vid�o de droite est sur 0, en train de prendre son retard.
                        // On la relance si celle de gauche a fait son d�calage.

                        // Attention, ne pas relancer si celle de gauche est en fait en train de terminer son tour
                        if (!m_bLeftIsCatchingUp && !m_bRightIsStarting)
                        {
                            EnsurePause(1);
                            m_iCurrentFrame = iLeftPosition;
                        }
                        else if (iLeftMilliseconds > (-m_iSyncLagMilliseconds) - 24)
                        {
                            // La vid�o de gauche est sur le point de franchir le sync point.
                            // les 24 ms suppl�mentaires sont pour tenir compte de l'inertie qu'� g�n�ralement
                            // la vid�o qui est partie en premier...
                            EnsurePlay(1);
                            m_bRightIsStarting = true;
                            m_bLeftIsCatchingUp = false;
                            m_iCurrentFrame = iLeftPosition;
                        }
                        else
                        {
                            // La vid�o de gauche n'a pas encore fait son d�calage.
                            // On ne force pas sa lecture. (Pause manuelle possible).
                            m_bLeftIsCatchingUp = true;
                            m_iCurrentFrame = iLeftPosition;
                        }
                    }
                }
                #endregion
                #region [0][0]
                else if (iLeftPosition == 0 && iRightPosition == 0)
                {
                    // Etat 1. [0][0]
                    m_iCurrentFrame = 0;

                    // Les deux vid�os viennent de boucler ou sont en train de repartir.
                    if (m_iSyncLag == 0)
                    {
                        //---------------------
                        // Redemmarrage commun.
                        //---------------------
                        if (!m_bLeftIsStarting && !m_bRightIsStarting)
                        {
                            EnsurePlay(0);
                            EnsurePlay(1);

                            m_bRightIsStarting = true;
                            m_bLeftIsStarting = true;
                        }
                    }
                    else if (m_iSyncLagMilliseconds > 0)
                    {
                        // Redemarrage uniquement de la vid�o de droite, 
                        // qui doit faire son d�calage

                        EnsurePause(0);
                        EnsurePlay(1);
                        m_bRightIsStarting = true;
                        m_bRightIsCatchingUp = true;
                    }
                    else if (m_iSyncLagMilliseconds < 0)
                    {
                        // Redemarrage uniquement de la vid�o de gauche, 
                        // qui doit faire son d�calage

                        EnsurePlay(0);
                        EnsurePause(1);
                        m_bLeftIsStarting = true;
                        m_bLeftIsCatchingUp = true;
                    }
                }
                #endregion
                #region [0][i]
                else if (iLeftPosition == 0 && iRightPosition > 0)
                {
                    // Etat [0][i]
                    EnsurePlay(1);
                    
                    m_bRightIsStarting = false;

                    if (m_iSyncLag == 0)
                    {
                        m_iCurrentFrame = iRightPosition;

                        //--------------------------------------------------------------------
                        // Configuration possible : la vid�o de gauche vient de boucler.
                        // On la stoppe en attendant le redemmarrage commun.
                        //--------------------------------------------------------------------
                        if (!m_bLeftIsStarting)
                        {
                            EnsurePause(0);
                        }
                    }
                    else if (m_iSyncLagMilliseconds > 0)
                    {
                        // La vid�o de gauche est sur 0, en train de prendre son retard.
                        // On la relance si celle de droite a fait son d�calage.

                        // Attention ne pas relancer si la vid�o de droite est en train de finir son tour
                        if (!m_bRightIsCatchingUp && !m_bLeftIsStarting)
                        {
                            // La vid�o de droite est en train de finir son tour tandisque celle de gauche a d�j� boucl�.
                            EnsurePause(0);
                            m_iCurrentFrame = iRightPosition;
                        }
                        else if (iRightMilliseconds > m_iSyncLagMilliseconds - 24)
                        {
                            // La vid�o de droite est sur le point de franchir le sync point.
                            // les 24 ms suppl�mentaires sont pour tenir compte de l'inertie qu'� g�n�ralement
                            // la vid�o qui est partie en premier...
                            EnsurePlay(0);
                            m_bLeftIsStarting = true;
                            m_bRightIsCatchingUp = false;
                            m_iCurrentFrame = iRightPosition;
                        }
                        else
                        {
                            // La vid�o de droite n'a pas encore fait son d�calage.
                            // On ne force pas sa lecture. (Pause manuelle possible).
                            m_bRightIsCatchingUp = true;
                            m_iCurrentFrame = iRightPosition;
                        }
                    }
                    else if (m_iSyncLagMilliseconds < 0)
                    {
                        // La vid�o de gauche est sur 0 et doit partir en premier.
                        // Si elle n'est pas en train de repartir, c'est qu'on 
                        // doit attendre que la vid�o de droite ait finit son tour.
                        if (!m_bLeftIsStarting)
                        {
                            EnsurePause(0);
                            m_iCurrentFrame = iRightPosition + m_iSyncLag;
                        }
                        else
                        {
                            // Rare, les deux premi�re frames de chaque vid�o n'arrivent pas en m�me temps
                            m_iCurrentFrame = iRightPosition;
                        }
                    }
                }
                #endregion
                #region [i][i]
                else
                {
                    // Etat [i][i]
                 	EnsurePlay(0);
                 	EnsurePlay(1);
                    
                    m_bLeftIsStarting = false;
                    m_bRightIsStarting = false;

                    m_iCurrentFrame = Math.Max(iLeftPosition, iRightPosition);
                }
                #endregion

                // Update position for trkFrame.
                object[] parameters = new object[] { m_iCurrentFrame };
                ((ScreenManagerUserInterface)UI).BeginInvoke(((ScreenManagerUserInterface)UI).m_DelegateUpdateTrkFrame, parameters);

                //log.Debug(String.Format("Tick:[{0}][{1}], Starting:[{2}][{3}], Catching up:[{4}][{5}]", iLeftPosition, iRightPosition, m_bLeftIsStarting, m_bRightIsStarting, m_bLeftIsCatchingUp, m_bRightIsCatchingUp));
            }
            else
            {
                // This can happen when a screen is closed on the fly while synching.
                StopDynamicSync();
                m_bSynching = false;
                ((ScreenManagerUserInterface)UI).DisplayAsPaused();
            }
        }
        private void EnsurePause(int _iScreen)
        {
        	//log.Debug(String.Format("Ensuring pause of screen [{0}]", _iScreen));
            if (_iScreen < screenList.Count)
            {
                if (((PlayerScreen)screenList[_iScreen]).IsPlaying)
                {
                    ((PlayerScreen)screenList[_iScreen]).m_PlayerScreenUI.OnButtonPlay();
                }
            }
            else
            {
                m_bSynching = false;
                ((ScreenManagerUserInterface)UI).DisplayAsPaused();
            }
        }
        private void EnsurePlay(int _iScreen)
        {
        	//log.Debug(String.Format("Ensuring play of screen [{0}]", _iScreen));
            if (_iScreen < screenList.Count)
            {
                if (!((PlayerScreen)screenList[_iScreen]).IsPlaying)
                {
                    ((PlayerScreen)screenList[_iScreen]).m_PlayerScreenUI.OnButtonPlay();
                }
            }
            else
            {
                m_bSynching = false;
                ((ScreenManagerUserInterface)UI).DisplayAsPaused();
            }
        }
        private void ResetDynamicSyncFlags()
        {
            m_bRightIsStarting = false;
            m_bLeftIsStarting = false;
            m_bLeftIsCatchingUp = false;
            m_bRightIsCatchingUp = false;
        }
        private void SyncCatch()
        {
            // We sync back the videos.
            // Used when one video has been moved individually.
			log.Debug("SyncCatch() called.");
            int iLeftFrame = ((PlayerScreen)screenList[0]).CurrentFrame;
            int iRightFrame = ((PlayerScreen)screenList[1]).CurrentFrame;

            if (m_iSyncLag > 0)
            {
                // Right video goes ahead.
                if (iLeftFrame + m_iSyncLag == m_iCurrentFrame || (m_iCurrentFrame < m_iSyncLag && iLeftFrame == 0))
                {
                    // Left video wasn't moved, we'll move it according to right video.
                    m_iCurrentFrame = iRightFrame;
                }
                else if (iRightFrame == m_iCurrentFrame)
                {
                    // Right video wasn't moved, we'll move it according to left video.
                    m_iCurrentFrame = iLeftFrame + m_iSyncLag;
                }
                else
                {
                    // Both videos were moved.
                    m_iCurrentFrame = iLeftFrame + m_iSyncLag;
                }
            }
            else
            {
                // Left video goes ahead.
                if (iRightFrame - m_iSyncLag == m_iCurrentFrame || (m_iCurrentFrame < -m_iSyncLag && iRightFrame == 0))
                {
                    // Right video wasn't moved, we'll move it according to left video.
                    m_iCurrentFrame = iLeftFrame;
                }
                else if (iLeftFrame == m_iCurrentFrame)
                {
                    // Left video wasn't moved, we'll move it according to right video.
                    m_iCurrentFrame = iRightFrame - m_iSyncLag;
                }
                else
                {
                    // Both videos were moved.
                    m_iCurrentFrame = iLeftFrame;
                }
            }

            OnCommonPositionChanged(m_iCurrentFrame);
            ((ScreenManagerUserInterface)UI).UpdateTrkFrame(m_iCurrentFrame);

        }
        #endregion

        #region Screens State Recalling
        public void StoreCurrentState()
        {
            //------------------------------------------------------------------------------
            // Before we start anything messy, let's store the current state of the ViewPort
            // So we can reinstate it later in case the user change his mind.
            //-------------------------------------------------------------------------------
            m_StoredStates.Add(GetCurrentState());
        }
        public ScreenManagerState GetCurrentState()
        {
            ScreenManagerState mState = new ScreenManagerState();

            foreach (AbstractScreen screen in screenList)
            {
                ScreenState state = new ScreenState();

                state.UniqueId = screen.UniqueId;

                if (screen is PlayerScreen)
                {
                    state.Loaded = screen.Full;
                    state.FilePath = ((PlayerScreen)screen).FilePath;
                    
                    if (state.Loaded)
                    {
                        state.MetadataString = ((PlayerScreen)screen).FrameServer.Metadata.ToXmlString();
                    }
                    else
                    {
                        state.MetadataString = "";
                    }
                }
                else
                {
                    state.Loaded = false;
                    state.FilePath = "";
                    state.MetadataString = "";
                }
                mState.ScreenList.Add(state);
            }

            return mState;
        }
        public void RecallState()
        {
            //-------------------------------------------------
            // Reconfigure the ViewPort to match the old state.
            // Reload the right movie with its meta data.
            //-------------------------------------------------
         
            if (m_StoredStates.Count > 0)
            {
                int iLastState = m_StoredStates.Count -1;
                CommandManager cm = CommandManager.Instance();
                ICommand css = new CommandShowScreens(this);

                ScreenManagerState CurrentState = GetCurrentState();

                switch (CurrentState.ScreenList.Count)
                {
                    case 0:
                        //-----------------------------
                        // Il y a actuellement 0 �cran.
                        //-----------------------------
                        switch (m_StoredStates[iLastState].ScreenList.Count)
                        {
                            case 0:
                                // Il n'y en avait aucun : Ne rien faire.
                                break;
                            case 1:
                                {
                                    // Il y en avait un : Ajouter l'�cran.
                                    ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 0, CurrentState); 
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            case 2:
                                {
                                    // Ajouter les deux �crans, on ne se pr�occupe pas trop de l'ordre
                                    ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 0, CurrentState);
                                    ReinstateScreen(m_StoredStates[iLastState].ScreenList[1], 1, CurrentState);
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    case 1:
                        //-----------------------------
                        // Il y a actuellement 1 �cran.
                        //-----------------------------
                        switch (m_StoredStates[iLastState].ScreenList.Count)
                        {
                            case 0:
                                {
                                    // Il n'y en avait aucun : Supprimer l'�cran.
                                    RemoveScreen(0);
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            case 1:
                                {
                                    // Il y en avait un : Remplacer si besoin.
                                    ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 0, CurrentState);
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            case 2:
                                {
                                    // Il y avait deux �cran : Comparer chaque ancien �cran avec le restant.
                                    int iMatchingScreen = -1;
                                    int i=0;
                                    while ((iMatchingScreen == -1) && (i < m_StoredStates[iLastState].ScreenList.Count))
                                    {
                                        if (m_StoredStates[iLastState].ScreenList[i].UniqueId == CurrentState.ScreenList[0].UniqueId)
                                        {
                                            iMatchingScreen = i;
                                        }
                                        else
                                        {
                                            i++;
                                        }
                                    }

                                    switch (iMatchingScreen)
                                    {
                                        case -1:
                                            {
                                                // No matching screen found
                                                ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 0, CurrentState);
                                                ReinstateScreen(m_StoredStates[iLastState].ScreenList[1], 1, CurrentState);
                                                break;
                                            }
                                        case 0:
                                            {
                                                // the old 0 is the new 0, the old 1 doesn't exist yet.
                                                ReinstateScreen(m_StoredStates[iLastState].ScreenList[1], 1, CurrentState);
                                                break;
                                            }
                                        case 1:
                                            {
                                                // the old 1 is the new 0, the old 0 doesn't exist yet.
                                                ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 1, CurrentState);
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    case 2:
                        // Il y a actuellement deux �crans.
                        switch (m_StoredStates[iLastState].ScreenList.Count)
                        {
                            case 0:
                                {
                                    // Il n'yen avait aucun : supprimer les deux.
                                    RemoveScreen(1);
                                    RemoveScreen(0);
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            case 1:
                                {
                                    // Il y en avait un : le rechercher parmi les nouveaux.
                                    int iMatchingScreen = -1;
                                    int i = 0;
                                    while ((iMatchingScreen == -1) && (i < CurrentState.ScreenList.Count))
                                    {
                                        if (m_StoredStates[iLastState].ScreenList[0].UniqueId == CurrentState.ScreenList[i].UniqueId)
                                        {
                                            iMatchingScreen = i;
                                        }
                                        
                                        i++;
                                    }

                                    switch (iMatchingScreen)
                                    {
                                        case -1:
                                            // L'ancien �cran n'a pas �t� retrouv�.
                                            // On supprime tout et on le rajoute.
                                            RemoveScreen(1);
                                            ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 0, CurrentState);
                                            break;
                                        case 0:
                                            // L'ancien �cran a �t� retrouv� dans l'�cran [0]
                                            // On supprime le second.
                                            RemoveScreen(1);
                                            break;
                                        case 1:
                                            // L'ancien �cran a �t� retrouv� dans l'�cran [1]
                                            // On supprime le premier.
                                            RemoveScreen(0);
                                            break;
                                        default:
                                            break;
                                    }
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            case 2:
                                {
                                    // Il y avait deux �crans �galement : Rechercher chacun parmi les nouveaux.
                                    int[] iMatchingScreen = new int[2];
                                    iMatchingScreen[0] = -1;
                                    iMatchingScreen[1] = -1;
                                    int i = 0;
                                    while (i < CurrentState.ScreenList.Count)
                                    {
                                        if (m_StoredStates[iLastState].ScreenList[0].UniqueId == CurrentState.ScreenList[i].UniqueId)
                                        {
                                            iMatchingScreen[0] = i;
                                        }
                                        else if (m_StoredStates[iLastState].ScreenList[1].UniqueId == CurrentState.ScreenList[i].UniqueId)
                                        {
                                            iMatchingScreen[1] = i;
                                        }

                                        i++;
                                    }

                                    switch (iMatchingScreen[0])
                                    {
                                        case -1:
                                            {
                                                // => L'ancien �cran [0] n'a pas �t� retrouv�.
                                                switch (iMatchingScreen[1])
                                                {
                                                    case -1:
                                                        {
                                                            // Aucun �cran n'a �t� retrouv�.
                                                            ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 0, CurrentState);
                                                            ReinstateScreen(m_StoredStates[iLastState].ScreenList[1], 1, CurrentState);
                                                            break;
                                                        }
                                                    case 0:
                                                        {
                                                            // Ecran 0 non retrouv�, �cran 1 retrouv� dans le 0.
                                                            // Remplacer l'�cran 1 par l'ancien 0.
                                                            ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 1, CurrentState);
                                                            break;
                                                        }
                                                    case 1:
                                                        {
                                                            // Ecran 0 non retrouv�, �cran 1 retrouv� dans le 1.
                                                            // Remplacer l'�cran 0.
                                                            ReinstateScreen(m_StoredStates[iLastState].ScreenList[0], 0, CurrentState);
                                                            break;
                                                        }
                                                    default:
                                                        break;
                                                }
                                                break;
                                            }
                                        case 0:
                                            {
                                                // L'ancien �cran [0] a �t� retrouv� dans l'�cran [0]
                                                switch (iMatchingScreen[1])
                                                {
                                                    case -1:
                                                        {
                                                            // Ecran 0 retrouv� dans le [0], �cran 1 non retrouv�. 
                                                            ReinstateScreen(m_StoredStates[iLastState].ScreenList[1], 1, CurrentState);
                                                            break;
                                                        }
                                                    case 0:
                                                        {
                                                            // Ecran 0 retrouv� dans le [0], �cran 1 retrouv� dans le [0].
                                                            // Impossible.
                                                            break;
                                                        }
                                                    case 1:
                                                        {
                                                            // Ecran 0 retrouv� dans le [0], �cran 1 retrouv� dans le [1].
                                                            // rien � faire.
                                                            break;
                                                        }
                                                    default:
                                                        break;
                                                }
                                                break;
                                            }
                                        case 1:
                                            {
                                                // L'ancien �cran [0] a �t� retrouv� dans l'�cran [1]
                                                switch (iMatchingScreen[1])
                                                {
                                                    case -1:
                                                        {
                                                            // Ecran 0 retrouv� dans le [1], �cran 1 non retrouv�. 
                                                            ReinstateScreen(m_StoredStates[iLastState].ScreenList[1], 0, CurrentState);
                                                            break;
                                                        }
                                                    case 0:
                                                        {
                                                            // Ecran 0 retrouv� dans le [1], �cran 1 retrouv� dans le [0].
                                                            // rien � faire (?)
                                                            break;
                                                        }
                                                    case 1:
                                                        {
                                                            // Ecran 0 retrouv� dans le [1], �cran 1 retrouv� dans le [1].
                                                            // Impossible
                                                            break;
                                                        }
                                                    default:
                                                        break;
                                                }
                                                break;
                                            }
                                        default:
                                            break;
                                    }
                                    CommandManager.LaunchCommand(css);
                                    break;
                                }
                            default:
                                break;
                        }
                        break;
                    default:
                        break;
                }

                // Once we have made such a recall, the Redo menu must be disabled...
                cm.BlockRedo();

                // Mettre � jour menus et Status bar
                UpdateStatusBar();
                OrganizeMenus();

                m_StoredStates.RemoveAt(iLastState);

            }
        }
        private void ReinstateScreen(ScreenState _OldScreen, int _iNewPosition, ScreenManagerState _CurrentState)
        {
            CommandManager cm = CommandManager.Instance();

            if (_iNewPosition > _CurrentState.ScreenList.Count - 1)
            {
                // We need a new screen.
                ICommand caps = new CommandAddPlayerScreen(this, false);
                CommandManager.LaunchCommand(caps);

                if (_OldScreen.Loaded)
                {
                    ReloadScreen(_OldScreen, _iNewPosition + 1);
                }
            }
            else
            {
                if (_OldScreen.Loaded)
                {
                    ReloadScreen(_OldScreen, _iNewPosition + 1);
                }
                else if (_CurrentState.ScreenList[_iNewPosition].Loaded)
                {
                    // L'ancien n'est pas charg� mais le nouveau l'est.
                    // => unload movie.
                    RemoveScreen(_iNewPosition);

                    ICommand caps = new CommandAddPlayerScreen(this, false);
                    CommandManager.LaunchCommand(caps);
                }
                else
                {
                    // L'ancien n'est pas charg�, le nouveau non plus.
                    // v�rifier que les deux sont bien des players...
                }
            }
        }
        private void RemoveScreen(int _iPosition)
        {
            CommandManager cm = CommandManager.Instance();

            ICommand crs = new CommandRemoveScreen(this, _iPosition, false);
            CommandManager.LaunchCommand(crs);

            if (m_bCancelLastCommand)
            {
                cm.UnstackLastCommand();
                m_bCancelLastCommand = false;
            }
        }
        private void ReloadScreen(ScreenState _OldScreen, int _iNewPosition)
        {
        	if(File.Exists(_OldScreen.FilePath))
            {
        		// We instantiate and launch it like a simple command (not undoable).
	            ICommand clmis = new CommandLoadMovieInScreen(this, _OldScreen.FilePath, _iNewPosition, false);
	            CommandManager.LaunchCommand(clmis);
	
	            // Check that everything went well 
	            // Potential problem : the video was deleted between do and undo.
	            // _iNewPosition should always point to a valid position here.
	            if (screenList[_iNewPosition-1].Full)
	            {
	            	PlayerScreen ps = m_ActiveScreen as PlayerScreen;
	            	if(ps != null)
	            	{
	                	ps.FrameServer.Metadata.LoadFromString(_OldScreen.MetadataString);
	                	ps.m_PlayerScreenUI.PostImportMetadata();
	            	}
	            }
        	}
        }
        #endregion
    }

	#region Global enums
	public enum VideoFilterType
	{
		AutoLevels,
		AutoContrast,
		Sharpen,
		EdgesOnly,
		Mosaic,
		Reverse,
		DebugSURF,
		NumberOfVideoFilters
	};	
    #endregion

}