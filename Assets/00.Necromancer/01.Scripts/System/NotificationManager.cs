using UnityEngine;
using System;
#if UNITY_ANDROID
using Unity.Notifications.Android;
using UnityEngine.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Necromancer.Systems
{
    /// <summary>
    /// [SYSTEM] 로컬 푸시 알림 관리자 (스태미나 회복 알림 등)
    /// GameManager에 의해 초기화 및 관리됩니다.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        private const string STAMINA_CHANNEL_ID = "stamina_recovery";
        private const string STAMINA_CHANNEL_NAME = "Stamina Recovery";
        private const string STAMINA_CHANNEL_DESC = "Notifications when your stamina is fully recovered.";

        public void Init()
        {
            Initialize();
        }

        private void Initialize()
        {
            #if UNITY_ANDROID
            // Android 13 (API 33) 이상 권한 요청
            if (Application.platform == RuntimePlatform.Android)
            {
                if (!Permission.HasUserAuthorizedPermission("android.permission.POST_NOTIFICATIONS"))
                {
                    Permission.RequestUserPermission("android.permission.POST_NOTIFICATIONS");
                }

                // 알림 채널 생성
                var channel = new AndroidNotificationChannel()
                {
                    Id = STAMINA_CHANNEL_ID,
                    Name = STAMINA_CHANNEL_NAME,
                    Importance = Importance.Default,
                    Description = STAMINA_CHANNEL_DESC,
                };
                AndroidNotificationCenter.RegisterNotificationChannel(channel);
            }
            #elif UNITY_IOS
            // iOS 권한 요청
            StartCoroutine(RequestIOSPermission());
            #endif
            Debug.Log("<color=cyan>[NotificationManager]</color> Initialized.");
        }

        #if UNITY_IOS
        private System.Collections.IEnumerator RequestIOSPermission()
        {
            using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, true))
            {
                while (!req.IsFinished)
                {
                    yield return null;
                }
            }
        }
        #endif

        /// <summary>
        /// 특정 시간(초) 뒤에 스태미나 회복 알림을 예약합니다.
        /// </summary>
        public void ScheduleStaminaNotification(int secondsDelay)
        {
            if (secondsDelay <= 0) return;

            CancelAllNotifications();

            string title = "스태미나 회복 완료!";
            string body = "네크로맨서님의 스태미나가 모두 회복되었습니다. 지금 바로 군단을 이끌어보세요!";

            #if UNITY_ANDROID
            var notification = new AndroidNotification
            {
                Title = title,
                Text = body,
                FireTime = DateTime.Now.AddSeconds(secondsDelay),
                SmallIcon = "icon_0",
                LargeIcon = "icon_1"
            };

            AndroidNotificationCenter.SendNotification(notification, STAMINA_CHANNEL_ID);
            Debug.Log($"[NotificationManager] Android Stamina Notification Scheduled in {secondsDelay}s");
            #elif UNITY_IOS
            var timeTrigger = new iOSNotificationTimeIntervalTrigger()
            {
                TimeInterval = new TimeSpan(0, 0, secondsDelay),
                Repeats = false
            };

            var notification = new iOSNotification()
            {
                Identifier = "_stamina_recovery_",
                Title = title,
                Body = body,
                ShowInForeground = true,
                ForegroundPresentationOption = (PresentationOption.Alert | PresentationOption.Sound),
                Trigger = timeTrigger
            };

            iOSNotificationCenter.ScheduleNotification(notification);
            Debug.Log($"[NotificationManager] iOS Stamina Notification Scheduled in {secondsDelay}s");
            #endif
        }

        /// <summary>
        /// 대기 중인 모든 알림을 취소합니다.
        /// </summary>
        public void CancelAllNotifications()
        {
            #if UNITY_ANDROID
            AndroidNotificationCenter.CancelAllNotifications();
            #elif UNITY_IOS
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
            #endif
            Debug.Log("[NotificationManager] All Notifications Cancelled");
        }
    }
}
