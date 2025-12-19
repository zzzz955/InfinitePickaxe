using System;
using System.Collections.Generic;
using UnityEngine;
using Infinitepickaxe;
using InfinitePickaxe.Client.Core;

namespace InfinitePickaxe.Client.Net
{
    /// <summary>
    /// 서버로부터 수신한 메시지를 처리하는 핸들러
    /// - 메시지 타입별 라우팅
    /// - 핸들러 등록/해제
    /// - UI 업데이트 이벤트 발행
    /// </summary>
    public class MessageHandler : MonoBehaviour
    {
        #region Singleton

        private static MessageHandler instance;
        public static MessageHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("MessageHandler");
                    instance = go.AddComponent<MessageHandler>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        #endregion

        #region Events - 메시지별 이벤트

        // 핸드셰이크
        public event Action<HandshakeResponse> OnHandshakeResult;

        // 유저 데이터
        public event Action<UserDataSnapshot> OnUserDataSnapshot;

        // 채굴
        public event Action<MiningUpdate> OnMiningUpdate;
        public event Action<MiningComplete> OnMiningComplete;

        // 광물
        public event Action<MineralListResponse> OnMineralListResponse;
        public event Action<ChangeMineralResponse> OnChangeMineralResponse;

        // 강화
        public event Action<UpgradeResult> OnUpgradeResult;

        // 슬롯
        public event Action<AllSlotsResponse> OnAllSlotsResponse;

        // 미션
        public event Action<DailyMissionsResponse> OnDailyMissionsResponse;
        public event Action<MissionProgressUpdate> OnMissionProgressUpdate;
        public event Action<MissionCompleteResult> OnMissionCompleteResult;
        public event Action<MissionRerollResult> OnMissionRerollResult;

        // 마일스톤
        public event Action<MilestoneClaimResult> OnMilestoneClaimResult;

        // 광고
        public event Action<AdWatchResult> OnAdWatchResult;

        // 재화
        public event Action<CurrencyUpdate> OnCurrencyUpdate;

        // 오프라인 보상
        public event Action<OfflineRewardResult> OnOfflineRewardResult;

        // 하트비트
        public event Action<HeartbeatAck> OnHeartbeat;

        // 에러
        public event Action<ErrorNotification> OnErrorNotification;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // NetworkManager 메시지 수신 이벤트 구독
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnMessageReceived += HandleMessage;
            }
        }

        private void OnDisable()
        {
            // NetworkManager 메시지 수신 이벤트 구독 해제
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.OnMessageReceived -= HandleMessage;
            }
        }

        #endregion

        #region Message Handling

        /// <summary>
        /// 수신한 Envelope 메시지를 타입별로 라우팅합니다
        /// </summary>
        private void HandleMessage(Envelope envelope)
        {
            if (envelope == null)
            {
                Debug.LogError("수신한 메시지가 null입니다.");
                return;
            }

            try
            {
                switch (envelope.Type)
                {
                    case MessageType.HandshakeResult:
                        HandleHandshakeResult(envelope.HandshakeResult);
                        break;

                    case MessageType.UserDataSnapshot:
                        HandleUserDataSnapshot(envelope.UserDataSnapshot);
                        break;

                    case MessageType.MiningUpdate:
                        HandleMiningUpdate(envelope.MiningUpdate);
                        break;

                    case MessageType.MiningComplete:
                        HandleMiningComplete(envelope.MiningComplete);
                        break;

                    case MessageType.MineralListResponse:
                        HandleMineralListResponse(envelope.MineralListResponse);
                        break;

                    case MessageType.ChangeMineralResponse:
                        HandleChangeMineralResponse(envelope.ChangeMineralResponse);
                        break;

                    case MessageType.UpgradeResult:
                        HandleUpgradeResult(envelope.UpgradeResult);
                        break;

                    case MessageType.AllSlotsResponse:
                        HandleAllSlotsResponse(envelope.AllSlotsResponse);
                        break;

                    case MessageType.DailyMissionsResponse:
                        HandleDailyMissionsResponse(envelope.DailyMissionsResponse);
                        break;

                    case MessageType.MissionProgressUpdate:
                        HandleMissionProgressUpdate(envelope.MissionProgressUpdate);
                        break;

                    case MessageType.MissionCompleteResult:
                        HandleMissionCompleteResult(envelope.MissionCompleteResult);
                        break;

                    case MessageType.MissionRerollResult:
                        HandleMissionRerollResult(envelope.MissionRerollResult);
                        break;

                    case MessageType.MilestoneClaimResult:
                        HandleMilestoneClaimResult(envelope.MilestoneClaimResult);
                        break;

                    case MessageType.AdWatchResult:
                        HandleAdWatchResult(envelope.AdWatchResult);
                        break;

                    case MessageType.CurrencyUpdate:
                        HandleCurrencyUpdate(envelope.CurrencyUpdate);
                        break;

                    case MessageType.OfflineRewardResult:
                        HandleOfflineRewardResult(envelope.OfflineRewardResult);
                        break;

                    case MessageType.HeartbeatAck:
                        HandleHeartbeat(envelope.HeartbeatAck);
                        break;

                    case MessageType.ErrorNotification:
                        HandleErrorNotification(envelope.ErrorNotification);
                        break;

                    default:
                        Debug.LogWarning($"처리되지 않은 메시지 타입: {envelope.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"메시지 처리 중 오류 발생: {envelope.Type}\n{ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        #region Message Handlers

        private void HandleHandshakeResult(HandshakeResponse result)
        {
            Debug.Log($"핸드셰이크 결과: {(result.Success ? "성공" : "실패")} - {result.Message}");
            if (result?.Snapshot != null)
            {
                PickaxeStateCache.Instance.UpdateFromSnapshot(result.Snapshot);
            }
            OnHandshakeResult?.Invoke(result);
        }

        private void HandleUserDataSnapshot(UserDataSnapshot snapshot)
        {
            Debug.Log($"유저 데이터 스냅샷 수신: Gold={snapshot.Gold ?? 0}, Crystal={snapshot.Crystal ?? 0}");
            PickaxeStateCache.Instance.UpdateFromSnapshot(snapshot);
            OnUserDataSnapshot?.Invoke(snapshot);
        }

        private void HandleMiningUpdate(MiningUpdate update)
        {
#if UNITY_EDITOR || DEBUG_NET
            int attackCount = update.Attacks?.Count ?? 0;
            ulong totalDamage = 0;
            if (attackCount > 0)
            {
                foreach (var attack in update.Attacks)
                {
                    totalDamage += attack.Damage;
                }
            }
            Debug.Log($"채굴 업데이트: 광물 #{update.MineralId}, HP {update.CurrentHp}/{update.MaxHp}, 공격 {attackCount}회, 총 데미지 {totalDamage}");
#endif
            OnMiningUpdate?.Invoke(update);
        }

        private void HandleMiningComplete(MiningComplete complete)
        {
            Debug.Log($"채굴 완료: 광물 #{complete.MineralId}, 획득 골드 {complete.GoldEarned}");
            OnMiningComplete?.Invoke(complete);
        }

        private void HandleMineralListResponse(MineralListResponse response)
        {
            Debug.Log($"광물 리스트 수신: {response.Minerals.Count}개");
            OnMineralListResponse?.Invoke(response);
        }

        private void HandleChangeMineralResponse(ChangeMineralResponse response)
        {
            if (response.Success)
            {
                Debug.Log($"광물 변경 성공: 광물 #{response.MineralId}");
            }
            else
            {
                Debug.LogWarning($"광물 변경 실패: {response.ErrorCode}");
            }
            OnChangeMineralResponse?.Invoke(response);
        }

        private void HandleUpgradeResult(UpgradeResult result)
        {
            CurrencyUpdate currencyUpdate = null;
            if (result != null)
            {
                currencyUpdate = new CurrencyUpdate
                {
                    Gold = result.RemainingGold,
                    Crystal = null,
                    Reason = "upgrade"
                };
            }

            if (result.Success)
            {
                Debug.Log($"강화 성공: 슬롯 #{result.SlotIndex}, Lv.{result.NewLevel}, DPS {result.NewDps}");
            }
            else
            {
                Debug.Log($"강화 실패: {result.ErrorCode}");
            }

            if (currencyUpdate != null)
            {
                OnCurrencyUpdate?.Invoke(currencyUpdate);
            }

            if (result != null)
            {
                PickaxeStateCache.Instance.UpdateFromUpgradeResult(result);
            }
            OnUpgradeResult?.Invoke(result);
        }

        private void HandleAllSlotsResponse(AllSlotsResponse response)
        {
            Debug.Log($"슬롯 정보 수신: {response.Slots.Count}개, Total DPS {response.TotalDps}");
            PickaxeStateCache.Instance.UpdateFromAllSlots(response);
            OnAllSlotsResponse?.Invoke(response);
        }

        private void HandleDailyMissionsResponse(DailyMissionsResponse response)
        {
            Debug.Log($"일일 미션 수신: {response.Missions.Count}개, 완료 {response.CompletedCount}, 리롤 {response.RerollCount}");
            OnDailyMissionsResponse?.Invoke(response);
        }

        private void HandleMissionProgressUpdate(MissionProgressUpdate update)
        {
#if UNITY_EDITOR || DEBUG_NET
            Debug.Log($"미션 진행도 업데이트: 슬롯 {update.SlotNo}, {update.CurrentValue}/{update.TargetValue}, 상태 {update.Status}");
#endif
            OnMissionProgressUpdate?.Invoke(update);
        }

        private void HandleMissionCompleteResult(MissionCompleteResult result)
        {
            if (result.Success)
            {
                Debug.Log($"미션 완료: 슬롯 {result.SlotNo}, 미션 {result.MissionId}, 보상 크리스탈 {result.RewardCrystal}, 총 크리스탈 {result.TotalCrystal}");
            }
            else
            {
                Debug.LogWarning($"미션 완료 실패: {result.ErrorCode}");
            }
            OnMissionCompleteResult?.Invoke(result);
        }

        private void HandleMissionRerollResult(MissionRerollResult result)
        {
            if (result.Success)
            {
                Debug.Log($"미션 리롤 성공: {result.RerolledMissions.Count}개 재생성");
            }
            else
            {
                Debug.LogWarning($"미션 리롤 실패: {result.ErrorCode}");
            }
            OnMissionRerollResult?.Invoke(result);
        }

        private void HandleMilestoneClaimResult(MilestoneClaimResult result)
        {
            if (result.Success)
            {
                Debug.Log($"마일스톤 보상 획득: {result.OfflineHoursGained}시간, 총 {result.TotalOfflineHours}시간");
            }
            else
            {
                Debug.LogWarning($"마일스톤 보상 실패: {result.ErrorCode}");
            }
            OnMilestoneClaimResult?.Invoke(result);
        }

        private void HandleAdWatchResult(AdWatchResult result)
        {
            if (result.Success)
            {
                Debug.Log($"광고 시청 완료: 타입 {result.AdType}, 크리스탈 {result.CrystalEarned}개 획득, 총 크리스탈 {result.TotalCrystal}");
            }
            else
            {
                Debug.LogWarning($"광고 시청 실패: {result.ErrorCode}");
            }
            OnAdWatchResult?.Invoke(result);
        }

        private void HandleCurrencyUpdate(CurrencyUpdate update)
        {
            Debug.Log($"재화 업데이트: Gold={update.Gold ?? 0}, Crystal={update.Crystal ?? 0}, 사유={update.Reason}");
            OnCurrencyUpdate?.Invoke(update);
        }

        private void HandleOfflineRewardResult(OfflineRewardResult result)
        {
            Debug.Log($"오프라인 보상: 골드 {result.GoldEarned}, 경과 시간 {result.ElapsedSeconds}초");
            OnOfflineRewardResult?.Invoke(result);
        }

        private void HandleHeartbeat(HeartbeatAck heartbeatAck)
        {
            // 클라이언트가 받는 하트비트는 실제로는 사용하지 않음
            // 서버는 HeartbeatAck를 보내야 함
#if UNITY_EDITOR || DEBUG_NET
            Debug.Log($"하트비트 수신");
#endif
            OnHeartbeat?.Invoke(heartbeatAck);
        }

        private void HandleErrorNotification(ErrorNotification error)
        {
            Debug.LogError($"서버 에러: [{error.ErrorCode}] {error.Message}");
            OnErrorNotification?.Invoke(error);
        }

        #endregion

        #region Public Helper Methods

        /// <summary>
        /// 광물 리스트 요청
        /// </summary>
        public void RequestMineralList()
        {
            var request = new MineralListRequest();
            var envelope = new Envelope
            {
                Type = MessageType.MineralListRequest,
                MineralListRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 광물 변경 요청
        /// </summary>
        public void RequestChangMineral(uint mineralId)
        {
            var request = new ChangeMineralRequest
            {
                MineralId = mineralId
            };
            var envelope = new Envelope
            {
                Type = MessageType.ChangeMineralRequest,
                ChangeMineralRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 곡괭이 강화 요청
        /// </summary>
        public void RequestUpgrade(uint slotIndex)
        {
            var request = new UpgradeRequest
            {
                SlotIndex = slotIndex
            };
            var envelope = new Envelope
            {
                Type = MessageType.UpgradeRequest,
                UpgradeRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 모든 슬롯 정보 요청
        /// </summary>
        public void RequestAllSlots()
        {
            var request = new AllSlotsRequest();
            var envelope = new Envelope
            {
                Type = MessageType.AllSlotsRequest,
                AllSlotsRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 일일 미션 리스트 요청
        /// </summary>
        public void RequestDailyMissions()
        {
            var request = new DailyMissionsRequest();
            var envelope = new Envelope
            {
                Type = MessageType.DailyMissionsRequest,
                DailyMissionsRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 미션 완료 보상 요청
        /// </summary>
        public void RequestMissionComplete(uint slotNo)
        {
            var request = new MissionComplete
            {
                SlotNo = slotNo
            };
            var envelope = new Envelope
            {
                Type = MessageType.MissionComplete,
                MissionComplete = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 미션 리롤 요청
        /// </summary>
        public void RequestMissionReroll()
        {
            var request = new MissionReroll();
            var envelope = new Envelope
            {
                Type = MessageType.MissionReroll,
                MissionReroll = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 마일스톤 보상 요청
        /// </summary>
        public void RequestMilestoneClaim(uint milestoneCount)
        {
            var request = new MilestoneClaim
            {
                MilestoneCount = milestoneCount
            };
            var envelope = new Envelope
            {
                Type = MessageType.MilestoneClaim,
                MilestoneClaim = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 광고 시청 완료 알림
        /// </summary>
        public void NotifyAdWatchComplete(string adType)
        {
            var request = new AdWatchComplete
            {
                AdType = adType
            };
            var envelope = new Envelope
            {
                Type = MessageType.AdWatchComplete,
                AdWatchComplete = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        #endregion
    }
}
