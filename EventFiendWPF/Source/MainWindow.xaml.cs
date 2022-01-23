using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace EventFiend
{
    public class EventLogs : ObservableCollection<EventLog> { }
    public class EventSources : ObservableCollection<EventSource> { }
    public class EventLogEntries : ObservableCollection<LogEntry> { }
    public class EventSource
    {
        public EventSource(string sourceParent, string name)
        {
            this.SourceParent = sourceParent;
            this.Name = name;
        }
        public string SourceParent { get; set; }
        public string Name { get; set; }
    }
    public class LogEntry
    {
        public LogEntry(string log, EventLogEntry entry)
        {
            this.Log = log;
            this.LogItem = entry;
        }
        public string Log { get; set; }
        public EventLogEntry LogItem { get; set; }
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            statusBarSystemText.Text = Environment.MachineName;
            eventLevelListBox.ItemsSource = Enum.GetValues(typeof(EventLogEntryType));
            RefreshEventLogs();
        }

        public string RemoteSystemName = string.Empty;

        private List<string> selectedFilterEventSources = new List<string>();

        private List<EventLogEntryType> selectedFilterLevels = new List<EventLogEntryType>();

        private string KeywordFilterText = string.Empty;

        public void RefreshEventLogs ()
        {
            // get all collections
            var eventLogs = Resources["eventLogs"] as EventLogs;
            var eventSources = Resources["eventSources"] as EventSources;
            var eventLogEntries = Resources["eventLogEntries"] as EventLogEntries;
            
            // clear all collections
            eventLogEntries.Clear();
            eventSources.Clear();            
            eventLogs.Clear();
            
            if (RemoteSystemName == string.Empty)
            {
                foreach (var log in EventLog.GetEventLogs())
                {
                    eventLogs.Add(log);
                }
            }
            if (RemoteSystemName != string.Empty)
            {
                try
                {
                    foreach (var log in EventLog.GetEventLogs(RemoteSystemName))
                    {
                        eventLogs.Add(log);
                    }
                    statusBarSystemText.Text = RemoteSystemName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    RemoteSystemName = string.Empty;
                }
            }
        }

        private bool IsMatchEventIds(LogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(textBoxEventIds.Text)) return true;
            if (textBoxEventIds.Text.Contains(','))
            {
                var eventIds = textBoxEventIds.Text.Split(',').ToList();
                if (eventIds.Count() > 0)
                {
                    if (eventIds.Contains(entry.LogItem.EventID.ToString())) return true;
                }
            }
            else
            {
                if (textBoxEventIds.Text.Contains(entry.LogItem.EventID.ToString())) return true;
            }
            return false;
        }

        private bool IsMatchMessages(LogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(textBoxMessages.Text)) return true;
            if (textBoxMessages.Text.Contains(','))
            {
                var words = textBoxMessages.Text.Split(',').ToList();
                if (words.Count() > 0)
                {
                    foreach (var w in words)
                    {
                        if (entry.LogItem.Message.Contains(w)) return true;
                    }
                }
            }
            else
            {
                if (entry.LogItem.Message.Contains(textBoxMessages.Text)) return true;
            }
            return false;
        }

        private bool IsMatchSources(LogEntry entry)
        {
            // check if entry has a selected source (if any source is selected it must pass this filter)
            if (selectedFilterEventSources.Count() == 0) return true;
            if (selectedFilterEventSources.Count() > 0)
            {
                if (selectedFilterEventSources.Contains(entry.LogItem.Source)) return true;
            }
            return false;
        }

        private bool IsMatchLevels(LogEntry entry)
        {
            if (selectedFilterLevels.Count == 0) return true;
            if (selectedFilterLevels.Count() > 0)
            {
                if (selectedFilterLevels.Contains(entry.LogItem.EntryType)) return true;
            }
            return false;
        }

        private bool IsMatchDateTime(LogEntry entry)
        {
            if (DateTimePickerFrom.Value == null && DateTimePickerTo.Value == null) return true;
            if (entry.LogItem.TimeWritten >= DateTimePickerFrom.Value && entry.LogItem.TimeWritten <= DateTimePickerTo.Value) return true;
            return false;
        }

        private bool IsNumberKey(Key inKey)
        {
            if (inKey < Key.D0 || inKey > Key.D9)
            {
                if (inKey < Key.NumPad0 || inKey > Key.NumPad9)
                {
                    return false;
                }
            }
            return true;
        }

        private bool IsDelOrBackspaceOrTabKey(Key inKey)
        {
            return inKey == Key.Delete || inKey == Key.Back || inKey == Key.Tab;
        }

        private void CollectionViewSource_Filter(object sender, FilterEventArgs e)
        {
            var entry = (LogEntry)e.Item;
            if (entry != null)
            {
                // perform filter checks // TODO: check date/time filters
                if (IsMatchSources(entry) && IsMatchMessages(entry) && IsMatchEventIds(entry) && IsMatchLevels(entry) && IsMatchDateTime(entry))
                {
                    e.Accepted = true;
                    return;
                }
                e.Accepted = false;
            }
        }

        private void btnRefreshEventLogs_Click(object sender, RoutedEventArgs e)
        {
            RefreshEventLogs();
        }

        private void checkBoxEventLog_Checked(object sender, RoutedEventArgs e)
        {
            // add all sources for the checked event log to the eventSourcesListBox ItemsSource
            try
            {
                var log = ((CheckBox)sender).DataContext as EventLog;
                var myEnum = log.Entries.GetEnumerator();
                var eventLogSources = new List<string>();
                var eventLogEntries = Resources["eventLogEntries"] as EventLogEntries;
                while (myEnum.MoveNext())
                {
                    eventLogSources.Add(((EventLogEntry)myEnum.Current).Source);
                    LogEntry entry = new LogEntry(log.Log, (EventLogEntry)myEnum.Current);
                    eventLogEntries.Add(entry);
                }
                var uniqueSources = eventLogSources.Distinct();
                var eventSources = Resources["eventSources"] as EventSources;
                foreach (var s in uniqueSources)
                {
                    var es = new EventSource(log.Log, s);
                    eventSources.Add(es);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                //if (ex.HResult == -2146233078)
                //{
                //    MessageBox.Show("Can't access the log, please make sure you're running this program elevated. Quitting.");
                //    Application.Current.Shutdown();
                //}
            }
        }

        private void checkBoxEventLog_Unchecked(object sender, RoutedEventArgs e)
        {
            // remove all sources for the unchecked event log from the eventSourcesListBox ItemsSource
            var log = ((CheckBox)sender).DataContext as EventLog;
            var eventSources = (EventSources)Resources["eventSources"];
            var eventSourcesToRemove = eventSources.Where(x => x.SourceParent == log.Log).ToList();
            var estrEnum = eventSourcesToRemove.GetEnumerator();
            while (estrEnum.MoveNext())
            {
                eventSources.Remove(estrEnum.Current);
            }

            // remove all event log entries for the unchecked log
            var eventLogEntries = (EventLogEntries)Resources["eventLogEntries"];
            var eventLogEntriesToRemove = eventLogEntries.Where(x => x.Log == log.Log).ToList();
            var eltrEnum = eventLogEntriesToRemove.GetEnumerator();
            while (eltrEnum.MoveNext())
            {
                eventLogEntries.Remove(eltrEnum.Current);
            }
        }

        private void btnConnectTo_Click(object sender, RoutedEventArgs e)
        {
            // show a dialog and get a remote system name to get event logs from
            var w = new ConnectToRemoteWindow();
            w.ShowDialog();
            if (!string.IsNullOrWhiteSpace(w.textBoxRemoteSystem.Text) && w.textBoxRemoteSystem.Text.Length != 0)
            {
                RemoteSystemName = w.textBoxRemoteSystem.Text;
                RefreshEventLogs();
            }
            else MessageBox.Show("No valid remote system name entered.");

        }

        private void checkBoxEventSource_Checked(object sender, RoutedEventArgs e)
        {
            // add the source to the gridview's event source filter checklist
            var es = (EventSource)((CheckBox)sender).DataContext;
            if (es != null) selectedFilterEventSources.Add(es.Name);
            CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource).Refresh();
        }

        private void checkBoxEventSource_Unchecked(object sender, RoutedEventArgs e)
        {
            // remove the source from the gridview's event source filter checklist
            if (((CheckBox)sender).DataContext is EventSource)
            {
                var es = (EventSource)((CheckBox)sender).DataContext;
                selectedFilterEventSources.Remove(es.Name);
                CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource).Refresh();
            }
        }

        private void radioButtonGroupBySource_Checked(object sender, RoutedEventArgs e)
        {
            // group the gridview by the event log entry sources
            ICollectionView cvEventLogEntries = CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource);
            if (cvEventLogEntries != null && cvEventLogEntries.CanGroup == true)
            {
                cvEventLogEntries.GroupDescriptions.Clear();
                cvEventLogEntries.GroupDescriptions.Add(new PropertyGroupDescription("LogItem.Source"));
            }
            if (cvEventLogEntries != null && cvEventLogEntries.CanSort == true)
            {
                cvEventLogEntries.SortDescriptions.Clear();
                cvEventLogEntries.SortDescriptions.Add(new SortDescription("LogItem.Source", ListSortDirection.Ascending));
                cvEventLogEntries.SortDescriptions.Add(new SortDescription("LogItem.TimeWritten", ListSortDirection.Ascending));
            }
        }

        private void radioButtonGroupByEventId_Checked(object sender, RoutedEventArgs e)
        {
            // group the gridview by the event log entry event id's
            ICollectionView cvEventLogEntries = CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource);
            if (cvEventLogEntries != null && cvEventLogEntries.CanGroup == true)
            {
                cvEventLogEntries.GroupDescriptions.Clear();
                cvEventLogEntries.GroupDescriptions.Add(new PropertyGroupDescription("LogItem.EventID"));
            }
            if (cvEventLogEntries != null && cvEventLogEntries.CanSort == true)
            {
                cvEventLogEntries.SortDescriptions.Clear();
                cvEventLogEntries.SortDescriptions.Add(new SortDescription("LogItem.EventID", ListSortDirection.Ascending));
                cvEventLogEntries.SortDescriptions.Add(new SortDescription("LogItem.TimeWritten", ListSortDirection.Ascending));
            }
        }

        private void radioButtonGroupByNone_Checked(object sender, RoutedEventArgs e)
        {
            // remove all grouping from the gridview
            if (eventLogEntriesDataGrid != null)
            {
                ICollectionView cvEventLogEntries = CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource);
                if (cvEventLogEntries != null && cvEventLogEntries.CanGroup == true)
                {
                    cvEventLogEntries.GroupDescriptions.Clear();
                }
            }
        }

        private void levelCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // add the level to the levels filter list
            var level = (EventLogEntryType)((CheckBox)sender).DataContext;
            selectedFilterLevels.Add(level);
            CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource).Refresh();
        }

        private void levelCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // remove the level from the levels filter list
            var level = (EventLogEntryType)((CheckBox)sender).DataContext;
            selectedFilterLevels.Remove(level);
            CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource).Refresh();
        }

        private void textBoxEventIds_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = !IsNumberKey(e.Key) && !IsDelOrBackspaceOrTabKey(e.Key);
        }

        private void btnApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            CollectionViewSource.GetDefaultView(eventLogEntriesDataGrid.ItemsSource).Refresh();
        }

        private void textBoxEventIds_TextChanged(object sender, TextChangedEventArgs e)
        {
            string tmp = textBoxEventIds.Text;
            foreach (char c in textBoxEventIds.Text.ToCharArray())
            {
                if (!Regex.IsMatch(c.ToString(), "^[0-9]*$"))
                {
                    tmp = tmp.Replace(c.ToString(), "");
                }
            }
            textBoxEventIds.Text = tmp;
        }
    }
}
