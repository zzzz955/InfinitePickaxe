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

        private ulong? lastGold;
        private uint? lastCrystal;
        private UserDataSnapshot lastSnapshot;

        /// <summary>
        /// 마지막으로 수신한 골드 값 (읽기 전용)
        /// </summary>
        public ulong? LastGold => lastGold;

        /// <summary>
        /// 마지막으로 수신한 크리스탈 값 (읽기 전용)
        /// </summary>
        public uint? LastCrystal => lastCrystal;

        // Gem pending requests (response에 consumed 정보가 없어서 request 추적 필요)
        private List<string> pendingSynthesisGemIds;
        private string pendingConversionGemId;
        private List<string> pendingDiscardGemIds;

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
        public event Action<SlotUnlockResult> OnSlotUnlockResult;

        // 미션
        public event Action<DailyMissionsResponse> OnDailyMissionsResponse;
        public event Action<MissionProgressUpdate> OnMissionProgressUpdate;
        public event Action<MissionCompleteResult> OnMissionCompleteResult;
        public event Action<MissionRerollResult> OnMissionRerollResult;

        // 마일스톤
        public event Action<MilestoneClaimResult> OnMilestoneClaimResult;
        public event Action<MilestoneState> OnMilestoneState;

        // 광고
        public event Action<AdWatchResult> OnAdWatchResult;
        public event Action<AdCountersState> OnAdCountersState;

        // 재화
        public event Action<CurrencyUpdate> OnCurrencyUpdate;

        // 오프라인 보상
        public event Action<OfflineRewardResult> OnOfflineRewardResult;

        // 하트비트
        public event Action<HeartbeatAck> OnHeartbeat;

        // 에러
        public event Action<ErrorNotification> OnErrorNotification;

        // 젬
        public event Action<GemListResponse> OnGemListResponse;
        public event Action<GemGachaResult> OnGemGachaResult;
        public event Action<GemSynthesisResult> OnGemSynthesisResult;
        public event Action<GemConversionResult> OnGemConversionResult;
        public event Action<GemDiscardResult> OnGemDiscardResult;
        public event Action<GemEquipResult> OnGemEquipResult;
        public event Action<GemUnequipResult> OnGemUnequipResult;
        public event Action<GemSlotUnlockResult> OnGemSlotUnlockResult;
        public event Action<GemInventoryExpandResult> OnGemInventoryExpandResult;

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

                    case MessageType.SlotUnlockResult:
                        HandleSlotUnlockResult(envelope.SlotUnlockResult);
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

                    case MessageType.MilestoneState:
                        HandleMilestoneState(envelope.MilestoneState);
                        break;

                    case MessageType.AdWatchResult:
                        HandleAdWatchResult(envelope.AdWatchResult);
                        break;

                    case MessageType.AdCountersState:
                        HandleAdCountersState(envelope.AdCountersState);
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

                    case MessageType.GemListResponse:
                        HandleGemListResponse(envelope.GemListResponse);
                        break;

                    case MessageType.GemGachaResult:
                        HandleGemGachaResult(envelope.GemGachaResult);
                        break;

                    case MessageType.GemSynthesisResult:
                        HandleGemSynthesisResult(envelope.GemSynthesisResult);
                        break;

                    case MessageType.GemConversionResult:
                        HandleGemConversionResult(envelope.GemConversionResult);
                        break;

                    case MessageType.GemDiscardResult:
                        HandleGemDiscardResult(envelope.GemDiscardResult);
                        break;

                    case MessageType.GemEquipResult:
                        HandleGemEquipResult(envelope.GemEquipResult);
                        break;

                    case MessageType.GemUnequipResult:
                        HandleGemUnequipResult(envelope.GemUnequipResult);
                        break;

                    case MessageType.GemSlotUnlockResult:
                        HandleGemSlotUnlockResult(envelope.GemSlotUnlockResult);
                        break;

                    case MessageType.GemInventoryExpandResult:
                        HandleGemInventoryExpandResult(envelope.GemInventoryExpandResult);
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
                CacheSnapshot(result.Snapshot);
                PickaxeStateCache.Instance.UpdateFromSnapshot(result.Snapshot);
                CacheCurrency(result.Snapshot.Gold, result.Snapshot.Crystal);
                if (result.Snapshot.ServerTime.HasValue)
                {
                    ServerTimeCache.Instance.Update(result.Snapshot.ServerTime.Value);
                }
                OnUserDataSnapshot?.Invoke(result.Snapshot);
            }
            if (result != null && result.Success)
            {
                QuestStateCache.Instance.ResetAll();
            }
            OnHandshakeResult?.Invoke(result);
        }

        private void HandleUserDataSnapshot(UserDataSnapshot snapshot)
        {
            Debug.Log($"유저 데이터 스냅샷 수신: Gold={snapshot.Gold ?? 0}, Crystal={snapshot.Crystal ?? 0}");
            CacheSnapshot(snapshot);
            PickaxeStateCache.Instance.UpdateFromSnapshot(snapshot);
            CacheCurrency(snapshot.Gold, snapshot.Crystal);
            if (snapshot.ServerTime.HasValue)
            {
                ServerTimeCache.Instance.Update(snapshot.ServerTime.Value);
            }
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
                CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
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

        private void HandleSlotUnlockResult(SlotUnlockResult result)
        {
            if (result == null)
            {
                Debug.LogWarning("슬롯 해금 결과가 null입니다.");
                return;
            }

            var currencyUpdate = new CurrencyUpdate
            {
                Gold = null,
                Crystal = result.RemainingCrystal,
                Reason = "slot_unlock"
            };

            if (result.Success)
            {
                Debug.Log($"슬롯 해금 성공: 슬롯 #{result.SlotIndex}, 사용 크리스탈 {result.CrystalSpent}, 남은 크리스탈 {result.RemainingCrystal}, Total DPS {result.TotalDps}");
                PickaxeStateCache.Instance.UpdateFromSlotUnlockResult(result);
                // 최신 스탯 동기화를 위해 전체 슬롯 정보 재요청
                RequestAllSlots();
            }
            else
            {
                Debug.Log($"슬롯 해금 실패: 슬롯 #{result.SlotIndex}, 에러 {result.ErrorCode}, 남은 크리스탈 {result.RemainingCrystal}");
            }

            CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
            OnCurrencyUpdate?.Invoke(currencyUpdate);
            OnSlotUnlockResult?.Invoke(result);
        }

        private void HandleDailyMissionsResponse(DailyMissionsResponse response)
        {
            Debug.Log($"일일 미션 수신: {response.Missions.Count}개, 완료 {response.CompletedCount}, 리롤 {response.RerollCount}");
            QuestStateCache.Instance.UpdateFromDailyMissionsResponse(response);
            OnDailyMissionsResponse?.Invoke(response);
        }

        private void HandleMissionProgressUpdate(MissionProgressUpdate update)
        {
#if UNITY_EDITOR || DEBUG_NET
            Debug.Log($"미션 진행도 업데이트: 슬롯 {update.SlotNo}, {update.CurrentValue}/{update.TargetValue}, 상태 {update.Status}");
#endif
            QuestStateCache.Instance.UpdateFromMissionProgress(update);
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
            if (result.Success || result.TotalCrystal > 0 || result.RewardCrystal > 0)
            {
                var currencyUpdate = new CurrencyUpdate
                {
                    Gold = null,
                    Crystal = result.TotalCrystal,
                    Reason = "mission_complete"
                };
                CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                OnCurrencyUpdate?.Invoke(currencyUpdate);
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
            if (result.Success || result.TotalCrystal > 0 || result.RewardCrystal > 0)
            {
                var currencyUpdate = new CurrencyUpdate
                {
                    Gold = null,
                    Crystal = result.TotalCrystal,
                    Reason = "milestone_claim"
                };
                CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                OnCurrencyUpdate?.Invoke(currencyUpdate);
            }
            OnMilestoneClaimResult?.Invoke(result);
        }

        private void HandleMilestoneState(MilestoneState state)
        {
            Debug.Log($"마일스톤 상태 수신: 완료 {state.CompletedCount}, 청구 {state.ClaimedMilestones.Count}");
            QuestStateCache.Instance.UpdateFromMilestoneState(state);
            OnMilestoneState?.Invoke(state);
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
            if (result.Success && (result.CrystalEarned > 0 || result.TotalCrystal > 0))
            {
                var currencyUpdate = new CurrencyUpdate
                {
                    Gold = null,
                    Crystal = result.TotalCrystal,
                    Reason = "ad_watch"
                };
                CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                OnCurrencyUpdate?.Invoke(currencyUpdate);
            }
            QuestStateCache.Instance.ApplyAdWatchResult(result);
            OnAdWatchResult?.Invoke(result);
        }

        private void HandleAdCountersState(AdCountersState state)
        {
            Debug.Log($"광고 카운터 상태 수신: {state.AdCounters.Count}개");
            QuestStateCache.Instance.UpdateFromAdCountersState(state);
            OnAdCountersState?.Invoke(state);
        }

        private void HandleCurrencyUpdate(CurrencyUpdate update)
        {
            Debug.Log($"재화 업데이트: Gold={update.Gold ?? 0}, Crystal={update.Crystal ?? 0}, 사유={update.Reason}");
            CacheCurrency(update.Gold, update.Crystal);
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

        private void HandleGemListResponse(GemListResponse response)
        {
            Debug.Log($"젬 리스트 수신: {response.Gems.Count}개, 인벤토리 용량 {response.InventoryCapacity}");
            GemStateCache.Instance.UpdateFromGemListResponse(response);
            // 장착 정보 동기화 (PickaxeStateCache로부터)
            GemStateCache.Instance.SyncEquippedGemsFromSlots();
            OnGemListResponse?.Invoke(response);
        }

        private void HandleGemGachaResult(GemGachaResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 뽑기 성공: {result.Gems.Count}개 획득, 남은 크리스탈 {result.RemainingCrystal}");
                GemStateCache.Instance.ApplyGachaResult(result);
                if (result.RemainingCrystal > 0)
                {
                    var currencyUpdate = new CurrencyUpdate
                    {
                        Gold = null,
                        Crystal = result.RemainingCrystal,
                        Reason = "gem_gacha"
                    };
                    CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                    OnCurrencyUpdate?.Invoke(currencyUpdate);
                }
            }
            else
            {
                Debug.LogWarning($"젬 뽑기 실패: {result.ErrorCode}");
            }
            OnGemGachaResult?.Invoke(result);
        }

        private void HandleGemSynthesisResult(GemSynthesisResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 합성 성공: 합성 {(result.SynthesisSuccess ? "성공" : "실패")}");
                if (pendingSynthesisGemIds != null)
                {
                    GemStateCache.Instance.RemoveGems(pendingSynthesisGemIds);
                    pendingSynthesisGemIds = null;
                }
                GemStateCache.Instance.ApplySynthesisResult(result);
            }
            else
            {
                Debug.LogWarning($"젬 합성 실패: {result.ErrorCode}");
                pendingSynthesisGemIds = null;
            }
            OnGemSynthesisResult?.Invoke(result);
        }

        private void HandleGemConversionResult(GemConversionResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 타입 전환 성공: 크리스탈 {result.CrystalSpent}개 사용");
                GemStateCache.Instance.ApplyConversionResult(result, pendingConversionGemId);
                pendingConversionGemId = null;
                if (result.RemainingCrystal > 0)
                {
                    var currencyUpdate = new CurrencyUpdate
                    {
                        Gold = null,
                        Crystal = result.RemainingCrystal,
                        Reason = "gem_conversion"
                    };
                    CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                    OnCurrencyUpdate?.Invoke(currencyUpdate);
                }
            }
            else
            {
                Debug.LogWarning($"젬 타입 전환 실패: {result.ErrorCode}");
                pendingConversionGemId = null;
            }
            OnGemConversionResult?.Invoke(result);
        }

        private void HandleGemDiscardResult(GemDiscardResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 분해 성공: 크리스탈 {result.CrystalEarned}개 획득");
                GemStateCache.Instance.ApplyDiscardResult(result, pendingDiscardGemIds);
                pendingDiscardGemIds = null;
                if (result.TotalCrystal > 0)
                {
                    var currencyUpdate = new CurrencyUpdate
                    {
                        Gold = null,
                        Crystal = result.TotalCrystal,
                        Reason = "gem_discard"
                    };
                    CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                    OnCurrencyUpdate?.Invoke(currencyUpdate);
                }
            }
            else
            {
                Debug.LogWarning($"젬 분해 실패: {result.ErrorCode}");
                pendingDiscardGemIds = null;
            }
            OnGemDiscardResult?.Invoke(result);
        }

        private void HandleGemEquipResult(GemEquipResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 장착 성공: 곡괭이 슬롯 {result.PickaxeSlotIndex}, 젬 슬롯 {result.GemSlotIndex}");
                GemStateCache.Instance.ApplyEquipResult(result);
            }
            else
            {
                Debug.LogWarning($"젬 장착 실패: {result.ErrorCode}");
            }
            OnGemEquipResult?.Invoke(result);
        }

        private void HandleGemUnequipResult(GemUnequipResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 장착 해제 성공: 곡괭이 슬롯 {result.PickaxeSlotIndex}, 젬 슬롯 {result.GemSlotIndex}");
                GemStateCache.Instance.ApplyUnequipResult(result);
            }
            else
            {
                Debug.LogWarning($"젬 장착 해제 실패: {result.ErrorCode}");
            }
            OnGemUnequipResult?.Invoke(result);
        }

        private void HandleGemSlotUnlockResult(GemSlotUnlockResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 슬롯 해금 성공: 곡괭이 슬롯 {result.PickaxeSlotIndex}, 젬 슬롯 {result.GemSlotIndex}, 사용 크리스탈 {result.CrystalSpent}");
                GemStateCache.Instance.ApplySlotUnlockResult(result);
                if (result.RemainingCrystal > 0)
                {
                    var currencyUpdate = new CurrencyUpdate
                    {
                        Gold = null,
                        Crystal = result.RemainingCrystal,
                        Reason = "gem_slot_unlock"
                    };
                    CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                    OnCurrencyUpdate?.Invoke(currencyUpdate);
                }
            }
            else
            {
                Debug.LogWarning($"젬 슬롯 해금 실패: {result.ErrorCode}");
            }
            OnGemSlotUnlockResult?.Invoke(result);
        }

        private void HandleGemInventoryExpandResult(GemInventoryExpandResult result)
        {
            if (result.Success)
            {
                Debug.Log($"젬 인벤토리 확장 성공: {result.NewCapacity}칸으로 확장, 사용 크리스탈 {result.CrystalSpent}");
                GemStateCache.Instance.ApplyInventoryExpandResult(result);
                if (result.RemainingCrystal > 0)
                {
                    var currencyUpdate = new CurrencyUpdate
                    {
                        Gold = null,
                        Crystal = result.RemainingCrystal,
                        Reason = "gem_inventory_expand"
                    };
                    CacheCurrency(currencyUpdate.Gold, currencyUpdate.Crystal);
                    OnCurrencyUpdate?.Invoke(currencyUpdate);
                }
            }
            else
            {
                Debug.LogWarning($"젬 인벤토리 확장 실패: {result.ErrorCode}");
            }
            OnGemInventoryExpandResult?.Invoke(result);
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
        /// 슬롯 해금 요청
        /// </summary>
        public void RequestSlotUnlock(uint slotIndex)
        {
            var request = new SlotUnlock
            {
                SlotIndex = slotIndex
            };
            var envelope = new Envelope
            {
                Type = MessageType.SlotUnlock,
                SlotUnlock = request
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

        /// <summary>
        /// 젬 리스트 요청
        /// </summary>
        public void RequestGemList()
        {
            var request = new GemListRequest();
            var envelope = new Envelope
            {
                Type = MessageType.GemListRequest,
                GemListRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 뽑기 요청
        /// </summary>
        public void RequestGemGacha(uint pullCount)
        {
            var request = new GemGachaRequest
            {
                PullCount = pullCount
            };
            var envelope = new Envelope
            {
                Type = MessageType.GemGachaRequest,
                GemGachaRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 합성 요청
        /// </summary>
        public void RequestGemSynthesis(string gemInstanceId1, string gemInstanceId2, string gemInstanceId3)
        {
            // pending 추적 (response에 consumed ID 없음)
            pendingSynthesisGemIds = new System.Collections.Generic.List<string>
            {
                gemInstanceId1,
                gemInstanceId2,
                gemInstanceId3
            };

            var request = new GemSynthesisRequest();
            request.GemInstanceIds.Add(gemInstanceId1);
            request.GemInstanceIds.Add(gemInstanceId2);
            request.GemInstanceIds.Add(gemInstanceId3);
            var envelope = new Envelope
            {
                Type = MessageType.GemSynthesisRequest,
                GemSynthesisRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 타입 전환 요청
        /// </summary>
        public void RequestGemConversion(string gemInstanceId, Infinitepickaxe.GemType targetType, bool useFixedCost)
        {
            // pending 추적 (response에 원본 gem ID 없음)
            pendingConversionGemId = gemInstanceId;

            var request = new GemConversionRequest
            {
                GemInstanceId = gemInstanceId,
                TargetType = targetType,
                UseFixedCost = useFixedCost
            };
            var envelope = new Envelope
            {
                Type = MessageType.GemConversionRequest,
                GemConversionRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 분해 요청
        /// </summary>
        public void RequestGemDiscard(System.Collections.Generic.List<string> gemInstanceIds)
        {
            // pending 추적 (response에 분해된 gem ID 목록 없음)
            pendingDiscardGemIds = new System.Collections.Generic.List<string>(gemInstanceIds);

            var request = new GemDiscardRequest();
            request.GemInstanceIds.AddRange(gemInstanceIds);
            var envelope = new Envelope
            {
                Type = MessageType.GemDiscardRequest,
                GemDiscardRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 장착 요청
        /// </summary>
        public void RequestGemEquip(uint pickaxeSlotIndex, uint gemSlotIndex, string gemInstanceId)
        {
            var request = new GemEquipRequest
            {
                PickaxeSlotIndex = pickaxeSlotIndex,
                GemSlotIndex = gemSlotIndex,
                GemInstanceId = gemInstanceId
            };
            var envelope = new Envelope
            {
                Type = MessageType.GemEquipRequest,
                GemEquipRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 장착 해제 요청
        /// </summary>
        public void RequestGemUnequip(uint pickaxeSlotIndex, uint gemSlotIndex)
        {
            var request = new GemUnequipRequest
            {
                PickaxeSlotIndex = pickaxeSlotIndex,
                GemSlotIndex = gemSlotIndex
            };
            var envelope = new Envelope
            {
                Type = MessageType.GemUnequipRequest,
                GemUnequipRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 슬롯 해금 요청
        /// </summary>
        public void RequestGemSlotUnlock(uint pickaxeSlotIndex, uint gemSlotIndex)
        {
            var request = new GemSlotUnlockRequest
            {
                PickaxeSlotIndex = pickaxeSlotIndex,
                GemSlotIndex = gemSlotIndex
            };
            var envelope = new Envelope
            {
                Type = MessageType.GemSlotUnlockRequest,
                GemSlotUnlockRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        /// <summary>
        /// 젬 인벤토리 확장 요청
        /// </summary>
        public void RequestGemInventoryExpand()
        {
            var request = new GemInventoryExpandRequest();
            var envelope = new Envelope
            {
                Type = MessageType.GemInventoryExpandRequest,
                GemInventoryExpandRequest = request
            };
            NetworkManager.Instance.SendMessage(envelope);
        }

        public bool TryGetLastCurrency(out ulong? gold, out uint? crystal)
        {
            gold = lastGold;
            crystal = lastCrystal;
            return gold.HasValue || crystal.HasValue;
        }

        public bool TryGetLastSnapshot(out UserDataSnapshot snapshot)
        {
            snapshot = lastSnapshot;
            return snapshot != null;
        }

        private void CacheCurrency(ulong? gold, uint? crystal)
        {
            if (gold.HasValue) lastGold = gold.Value;
            if (crystal.HasValue) lastCrystal = crystal.Value;
        }

        private void CacheSnapshot(UserDataSnapshot snapshot)
        {
            if (snapshot != null)
            {
                lastSnapshot = snapshot;
            }
        }

        #endregion
    }
}
