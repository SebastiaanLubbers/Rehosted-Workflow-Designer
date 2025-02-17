﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Activities;
using System.Activities.Presentation.Toolbox;
using System.Reflection;
using System.IO;
using System.Activities.XamlIntegration;
using Microsoft.Win32;
using MeetupWfIntro.Helpers;
using System.Windows.Controls.Ribbon;

namespace MeetupWfIntro.Views
{

    public partial class MainWindow
    {
        private WorkflowApplication _wfApp;
        private ToolboxControl _wfToolbox;
        private CustomTrackingParticipant _executionLog;

        private string _currentWorkflowFile = string.Empty;


        public MainWindow()
        {
            InitializeComponent();

            //load all available workflow activities from loaded assemblies 
            InitializeActivitiesToolbox();

            //initialize designer
            WfDesignerBorder.Child = CustomWfDesigner.Instance.View;
            WfPropertyBorder.Child = CustomWfDesigner.Instance.PropertyInspectorView;

        }


        /// <summary>
        /// Retrieves all Workflow Activities from the loaded assemblies and inserts them into a ToolboxControl 
        /// </summary>
        private void InitializeActivitiesToolbox()
        {
            try
            {
                _wfToolbox = new ToolboxControl();

                // load Custom Activity Libraries into current domain
                AppDomain.CurrentDomain.Load("MeetupActivityLibrary");

                // get all loaded assemblies
                IEnumerable<Assembly> appAssemblies = AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name);

                // check if assemblies contain activities
                int activitiesCount = 0;
                foreach (Assembly activityLibrary in appAssemblies)
                {
                    var wfToolboxCategory = new ToolboxCategory(activityLibrary.GetName().Name);
                    var actvities = from
                                        activityType in activityLibrary.GetExportedTypes()
                                    where
                                        (activityType.IsSubclassOf(typeof(Activity))
                                        || activityType.IsSubclassOf(typeof(NativeActivity))
                                        || activityType.IsSubclassOf(typeof(DynamicActivity))
                                        || activityType.IsSubclassOf(typeof(ActivityWithResult))
                                        || activityType.IsSubclassOf(typeof(AsyncCodeActivity))
                                        || activityType.IsSubclassOf(typeof(CodeActivity))
                                        || activityType == typeof(System.Activities.Core.Presentation.Factories.ForEachWithBodyFactory<Type>)
                                        || activityType == typeof(System.Activities.Statements.FlowNode)
                                        || activityType == typeof(System.Activities.Statements.State)
                                        || activityType == typeof(System.Activities.Core.Presentation.FinalState)
                                        || activityType == typeof(System.Activities.Statements.FlowDecision)
                                        || activityType == typeof(System.Activities.Statements.FlowNode)
                                        || activityType == typeof(System.Activities.Statements.FlowStep)
                                        || activityType == typeof(System.Activities.Statements.FlowSwitch<Type>)
                                        || activityType == typeof(System.Activities.Statements.ForEach<Type>)
                                        || activityType == typeof(System.Activities.Statements.Switch<Type>)
                                        || activityType == typeof(System.Activities.Statements.TryCatch)
                                        || activityType == typeof(System.Activities.Statements.While))
                                        && activityType.IsVisible
                                        && activityType.IsPublic
                                        && !activityType.IsNested
                                        && !activityType.IsAbstract
                                        && (activityType.GetConstructor(Type.EmptyTypes) != null)
                                        && !activityType.Name.Contains('`')
                                    orderby
                                        activityType.Name
                                    select
                                        new ToolboxItemWrapper(activityType);

                    actvities.ToList().ForEach(wfToolboxCategory.Add);
                    if (wfToolboxCategory.Tools.Count > 0)
                    {
                        _wfToolbox.Categories.Add(wfToolboxCategory);
                        activitiesCount += wfToolboxCategory.Tools.Count;
                    }
                }
                LabelStatusBar.Content = String.Format("Loaded Activities: {0}", activitiesCount.ToString());
                WfToolboxBorder.Child = _wfToolbox;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        /// <summary>
        /// Retrieve Workflow Execution Logs and Workflow Execution Outputs
        /// </summary>
        private void WfExecutionCompleted(WorkflowApplicationCompletedEventArgs ev)
        {
            try
            {
                //retrieve & display execution log
                consoleExecutionLog.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(
                        delegate()
                        {
                            consoleExecutionLog.Text = _executionLog.TrackData;
                        }
                ));

                //retrieve & display execution output
                foreach (var item in ev.Outputs)
                {
                    consoleOutput.Dispatcher.Invoke(
                        System.Windows.Threading.DispatcherPriority.Normal,
                        new Action(
                            delegate()
                            {
                                consoleOutput.Text += String.Format("[{0}] {1}" + Environment.NewLine, item.Key, item.Value);
                            }
                    ));
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }



        #region Commands Handlers - Executed - New, Open, Save, Run

        /// <summary>
        /// Creates a new Workflow Application instance and executes the Current Workflow
        /// </summary>
        private void CmdWorkflowRun(object sender, ExecutedRoutedEventArgs e)
        {           
            //get workflow source from designer
            CustomWfDesigner.Instance.Flush();
            MemoryStream workflowStream = new MemoryStream(ASCIIEncoding.Default.GetBytes(CustomWfDesigner.Instance.Text));
            DynamicActivity activityExecute = ActivityXamlServices.Load(workflowStream) as DynamicActivity;

            //configure workflow application
            consoleExecutionLog.Text = String.Empty;
            consoleOutput.Text = String.Empty;
            _executionLog = new CustomTrackingParticipant();
            _wfApp = new WorkflowApplication(activityExecute);
            _wfApp.Extensions.Add(_executionLog);
            _wfApp.Completed = WfExecutionCompleted;

            //execute 
            _wfApp.Run();
        }


        /// <summary>
        /// Save the current state of a Workflow
        /// </summary>
        private void CmdWorkflowSave(object sender, ExecutedRoutedEventArgs e)
        {
            if (_currentWorkflowFile == String.Empty)
            {
                var dialogSave = new SaveFileDialog();
                dialogSave.Title = "Save Workflow";
                dialogSave.Filter = "Workflows (.xaml)|*.xaml";

                if (dialogSave.ShowDialog() == true)
                {
                    CustomWfDesigner.Instance.Save(dialogSave.FileName);
                        _currentWorkflowFile = dialogSave.FileName;                
                }
            }
            else
            {
                CustomWfDesigner.Instance.Save(_currentWorkflowFile);
            }
        }


        /// <summary>
        /// Creates a new Workflow Designer instance and loads the Default Workflow 
        /// </summary>
        private void CmdWorkflowNew(object sender, ExecutedRoutedEventArgs e)
        {
            _currentWorkflowFile = String.Empty;
            CustomWfDesigner.NewInstance();
            WfDesignerBorder.Child = CustomWfDesigner.Instance.View;
            WfPropertyBorder.Child = CustomWfDesigner.Instance.PropertyInspectorView;
        }


        /// <summary>
        /// Loads a Workflow into a new Workflow Designer instance
        /// </summary>
        private void CmdWorkflowOpen(object sender, ExecutedRoutedEventArgs e)
        {            
            var dialogOpen = new OpenFileDialog();
            dialogOpen.Title = "Open Workflow";
            dialogOpen.Filter = "Workflows (.xaml)|*.xaml";

            if (dialogOpen.ShowDialog() == true)
            {
                using (var file = new StreamReader(dialogOpen.FileName, true))
                {
                    CustomWfDesigner.NewInstance(dialogOpen.FileName);
                    WfDesignerBorder.Child = CustomWfDesigner.Instance.View;
                    WfPropertyBorder.Child = CustomWfDesigner.Instance.PropertyInspectorView;

                    _currentWorkflowFile = dialogOpen.FileName;
                }
            }
        }

        #endregion
    }
}
