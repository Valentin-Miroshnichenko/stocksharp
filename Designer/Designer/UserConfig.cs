namespace StockSharp.Designer
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;

	using Ecng.Collections;
	using Ecng.Common;
	using Ecng.Configuration;
	using Ecng.Serialization;
	using Ecng.Xaml;

	using StockSharp.Logging;
	using StockSharp.Studio.Core.Services;
	using StockSharp.Xaml;

	class UserConfig : BaseLogReceiver, IPersistableService
	{
		#region Static

		public static UserConfig Instance { get; private set; }

		static UserConfig()
		{
			Instance = new UserConfig();
		}

		#endregion

		private readonly TimeSpan _period = TimeSpan.FromSeconds(20);
		private readonly SyncObject _disposeSync = new SyncObject();
		private readonly object _syncRoot = new object();

		private readonly string _settingsFile;
		private readonly string _logSettingsFile;

		private Timer _flushTimer;
		private bool _isFlushing;
		private bool _isLayoutChanged;
		private bool _isChanged;
		private bool _isReseting;
		private bool _isDisposing;

		private SettingsStorage _storage;

		private SettingsStorage Storage
		{
			get
			{
				lock (this)
				{
					if (_storage != null)
						return _storage;

					try
					{
						if (File.Exists(_settingsFile))
							_storage = CultureInfo.InvariantCulture.DoInCulture(() => new XmlSerializer<SettingsStorage>().Deserialize(_settingsFile));
					}
					catch (Exception ex)
					{
						this.AddErrorLog(ex);
					}

					return _storage ?? (_storage = new SettingsStorage());
				}
			}
		}

		public bool IsChangesSuspended { get; private set; }

		public string LogsDir { get; private set; }

		public Func<SettingsStorage> SaveLayout { get; set; }

		public UserConfig()
		{
			Directory.CreateDirectory(BaseApplication.AppDataPath);

			_settingsFile = Path.Combine(BaseApplication.AppDataPath, "settings.xml");
			_logSettingsFile = Path.Combine(BaseApplication.AppDataPath, "logManager.xml");

			LogsDir = Path.Combine(BaseApplication.AppDataPath, "Logs");
		}

		public void SuspendChangesMonitor()
		{
			IsChangesSuspended = true;
		}

		public void ResumeChangesMonitor()
		{
			IsChangesSuspended = false;
		}

		public void MarkLayoutChanged()
		{
			_isLayoutChanged = true;
			Flush();
		}

		public void ResetSettings()
		{
			_isReseting = true;

			ConfigManager.GetService<LogManager>().Dispose();
			Directory.Delete(BaseApplication.AppDataPath, true);
		}

		public LogManager CreateLogger()
		{
			var logManager = new LogManager();

			var serializer = new XmlSerializer<SettingsStorage>();

			if (File.Exists(_logSettingsFile))
			{
				logManager.Load(serializer.Deserialize(_logSettingsFile));

				var listener = logManager
					.Listeners
					.OfType<FileLogListener>()
					.FirstOrDefault(fl => !fl.LogDirectory.IsEmpty());

				if (listener != null)
					LogsDir = listener.LogDirectory;
			}
			else
			{
				logManager.Listeners.Add(new FileLogListener/*(LoggerErrorFileName)*/
				{
					Append = true,
					LogDirectory = LogsDir,
					MaxLength = 1024 * 1024 * 100 /* 100mb */,
					MaxCount = 10,
					SeparateByDates = SeparateByDateModes.SubDirectories,
				});

				serializer.Serialize(logManager.Save(), _logSettingsFile);
			}

			return logManager;
		}

		protected override void DisposeManaged()
		{
			if (!_isReseting)
			{
				lock (_syncRoot)
				{
					_isDisposing = true;
					_isLayoutChanged = true;

					Flush();
					_flushTimer.Change(TimeSpan.Zero, _period);
				}

				_disposeSync.WaitSignal();
			}

			base.DisposeManaged();
		}

		private void Flush()
		{
			lock (_syncRoot)
			{
				if (_isFlushing || _flushTimer != null)
					return;

				_flushTimer = new Timer(OnFlush, null, _period, _period);
			}
		}

		private void OnFlush(object state)
		{
			SettingsStorage clone;
			bool isLayoutChanged;
			bool isChanged;

			lock (_syncRoot)
			{
				if (_isFlushing)
					return;

				isLayoutChanged = _isLayoutChanged;
				isChanged = _isChanged;

				_isFlushing = true;
				_isLayoutChanged = false;
				_isChanged = false;

				clone = new SettingsStorage();
				clone.AddRange(Storage);
			}

			try
			{
				if (isChanged || isLayoutChanged)
				{
					if (isLayoutChanged)
						clone.SetValue("Layout", SaveLayout?.Invoke());

					try
					{
						CultureInfo.InvariantCulture.DoInCulture(() => new XmlSerializer<SettingsStorage>().Serialize(clone, _settingsFile));
					}
					catch (Exception ex)
					{
						this.AddErrorLog(ex);
					}

					if (_isDisposing)
						_disposeSync.PulseSignal();
				}
				else
				{
					lock (_syncRoot)
					{
						if (_flushTimer == null)
							return;

						_flushTimer.Dispose();
						_flushTimer = null;
					}
				}
			}
			catch (Exception excp)
			{
				this.AddErrorLog(excp, "Flush layout changed error.");
			}
			finally
			{
				lock (_syncRoot)
					_isFlushing = false;
			}
		}

		#region IPersistableService

		public bool ContainsKey(string key)
		{
			return Storage.ContainsKey(key);
		}

		public TValue GetValue<TValue>(string key, TValue defaultValue)
		{
			return Storage.GetValue(key, defaultValue);
		}

		public void SetValue(string key, object value)
		{
			if (IsChangesSuspended)
				return;

			lock (Storage.SyncRoot)
			{
				Storage.SetValue(key, value);
				_isChanged = true;
				Flush();
			}
		}

		#endregion
	}
}
