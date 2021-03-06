﻿using System;
using System.Windows;
using Gwupe.Agent.UI.WPF.API;

namespace Gwupe.Agent.UI.WPF
{
    /// <summary>
    /// Interaction logic for AlertControl.xaml
    /// </summary>
    public partial class AlertControl : GwupeModalUserControl
    {
        private readonly DashboardDataContext _dashboardDataContext;

        public AlertControl(DashboardDataContext dashboardDataContext)
        {
            InitializeComponent();
            _dashboardDataContext = dashboardDataContext;
            InitGwupeModalUserControl(Disabler, null, null);
        }

        protected override bool ValidateInput()
        {
            return true;
        }

        public void SetPrompt(String message)
        {
            Dispatcher.Invoke(new Action(() => AlertMessage.Text = message));
        }

        protected override void Show()
        {
            _dashboardDataContext.DashboardStateManager.EnableDashboardState(DashboardState.Alert);
        }

        protected override void Hide()
        {
            _dashboardDataContext.DashboardStateManager.DisableDashboardState(DashboardState.Alert);
        }

        protected override void ResetInputs()
        {
        }

        protected override bool CommitInput()
        {
            return true;
        }
    }
}
