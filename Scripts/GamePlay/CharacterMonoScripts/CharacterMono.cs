﻿using BehaviorDesigner.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 用于管理一个单位在游戏中的逻辑,如播放动画,播放音效,进行攻击等等操作.
/// </summary>
[RequireComponent(typeof(Animator), typeof(NavMeshAgent))]
public class CharacterMono : MonoBehaviour {
    protected SimpleCharacterViewModel simpleCharacterViewModel;
    private List<ItemViewModel> itemViewModels = new List<ItemViewModel>(6);
    public SimpleCharacterViewModel SimpleCharacterViewModel {
        get {
            return simpleCharacterViewModel;
        }

        set {
            simpleCharacterViewModel = value;
        }
    }
    public List<ItemViewModel> ItemViewModels {
        get {
            return itemViewModels;
        }

        set {
            itemViewModels = value;
        }
    }


    #region 小兵的WayPointUnit属性
    public WayPointsUnit wayPointsUnit = null;
    #endregion

    #region 单位身上的事件及隐藏的委托

    //======================================
    // 委托集合

    /// <summary>
    /// 当单位身上的状态改变时，触发的事件
    /// </summary>
    /// <param name="battleState">新状态</param>
    public delegate void BattleStatusChangedHandler(BattleState battleState);

    /// <summary>
    /// 当单位进行攻击时，或者遭受伤害时，触发的事件
    /// </summary>
    /// <param name="Attacker">攻击者</param>
    /// <param name="Suffered">遭受伤害者</param>
    /// <param name="Damage">此次攻击造成的伤害</param>
    public delegate void AttackHanlder(CharacterMono Attacker,CharacterMono Suffered,Damage damage);

    /// <summary>
    /// 当单位进行施法时，或者遭受某个施法的单位指向时，触发的事件
    /// </summary>
    /// <param name="Spller">施法者</param>
    /// <param name="Target">法术指定目标</param>
    /// <param name="damage">此次造成的伤害（为负则为治疗）</param>
    /// <param name="activeSkill">此次施法释放的主动技能（被动技能不算把？）</param>
    public delegate void SpellHanlder(CharacterMono Spller, CharacterMono Target, Damage damage,ActiveSkill activeSkill);
    //=====================================
    // 事件集合

    // 当单位身上增加新的状态(如中毒状态)时,触发的事件
    public event BattleStatusChangedHandler OnAddNewBattleStatus;
    public event BattleStatusChangedHandler OnRemoveBattleStatus;

    // 当单位攻击、遭受伤害时，触发的事件
    public event AttackHanlder OnAttack;        // 当攻击时
    public event AttackHanlder OnSuffered;      // 当单位遭受攻击时

    // 当单位施法时，触发的事件
    public event SpellHanlder OnSpell;

    #endregion

    // 当前人物的动画组件以及寻路组件
    private Animator animator;
    private NavMeshAgent agent;

    /// <summary>
    /// 表示当前单位的一些基本属性,如:hp,mp,攻击力等等
    /// </summary>
    public CharacterModel characterModel;
    // 此单位的所有者
    public Player Owner;

    #region GamePlay相关 包含一些用于战斗时的变量

    // 表示当前单位是否垂死
    public bool isDying = false;

    // 当前准备释放的技能
    public ActiveSkill prepareSkill = null;

    // 表示是否准备释放法术
    public bool isPrepareUseSkill = false;

    // 当前准备释放的技能是否是物品技能
    public bool isPrepareUseItemSkill = false;
    // 当前准备释放的物品技能的物品格子类
    public ItemGrid prepareItemSkillItemGrid;

    // 周围的敌人
    public List<CharacterMono> arroundEnemies;

    // 当前角色拥有的所有状态
    private List<BattleState> battleStates = new List<BattleState>();

    #endregion

    #region 用于处理单位身上的状态集合

    /// <summary>
    /// 为单位增加新状态
    /// </summary>
    /// <param name="newBattleState"></param>
    public void AddBattleState(BattleState newBattleState) {
        // 判断单位身上已经是否有这个状态了,并且判断状态是否可以叠加
        if (battleStates.Find((battleState) => { return battleState.GetType() == newBattleState.GetType(); }) == null ||
            newBattleState.isStackable) {
            battleStates.Add(newBattleState);


            // 触发单位状态附加事件,向所有订阅该事件的观察者发送消息
            if (OnAddNewBattleStatus != null)
                OnAddNewBattleStatus(newBattleState);
        }
    }

    /// <summary>
    /// 去除单位身上某一个状态
    /// </summary>
    /// <param name="battleState"></param>
    public void RemoveBattleState(BattleState battleState) {
        battleStates.Remove(battleState);

        if (OnRemoveBattleStatus != null)
            OnRemoveBattleStatus(battleState);
    }
    #endregion

    #region 测试
    //================================================
    // ●测试用
    public GameObject targetPositionEffect;
    public GameObject targetEnemryEffect;
    public ProjectileMono projectile;
    public GameObject stateHolderEffect;
    public void Install() {
        characterModel = new HeroModel {
            maxHp = 10000,
            Hp = 200,
            maxMp = 1000,
            Mp = 1000,
            name = "sjm",
            attackDistance = 10f,
            //projectileModel = new ProjectileModel {
            //    spherInfluence = 3f,
            //    targetPositionEffect = targetPositionEffect,
            //    tartgetEnemryEffect = targetEnemryEffect,
            //    movingSpeed = 4,
            //    turningSpeed = 1
            //},
            //projectile = projectile,
            Level = 0,
            forcePower = 100,
            needExp = 1000,
            attack = 100,
            Exp = 0,
            expfactor = 2,
            AvatarImagePath = "PlayerAvatarImage",
            agilePower = 20,
            intelligencePower = 10,
            mainAttribute = HeroMainAttribute.AGI,
            skillPointGrowthPoint = 1,
            turningSpeed = 5,
            activeSkills = new List<ActiveSkill> {
                new RangeDamageSkill{
                    damage = new Damage(){ BaseDamage=500,PlusDamage=100 },
                    KeyCode = KeyCode.E,
                    Mp = 10,
                    PlusDamage = 200,
                    SpellDistance = 7f,
                    CD = 2f,
                    SkillName = "E技能",
                    IconPath = "00046",
                    LongDescription = "one skill Description",
                    SkillLevel = 1,
                    SkillInfluenceRadius = 1,
                    IsMustDesignation = false
                },
                new PointingSkill{
                    BaseDamage = 1000,
                    KeyCode = KeyCode.W,
                    Mp = 220,
                    PlusDamage = 200,
                    SelfEffect = null,
                    TargetEffect = null,
                    SpellDistance = 4f,
                    CD = 0.5f,
                    SkillName = "W技能",
                    IconPath = "00041",
                    LongDescription = "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化," +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化" +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化" +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化",
                    BackgroundDescription = "aaaaaaaaaaaaaaa",
                    ShortDescription = "bbbbbbbbbbbbbbbbbbbbbbbbbb",
                    SkillLevel = 6,
                    SkillInfluenceRadius = 3,
                    IsMustDesignation = true
                },
                new PointingSkill{
                    BaseDamage = 1000,
                    KeyCode = KeyCode.Z,
                    Mp = 220,
                    PlusDamage = 200,
                    SelfEffect = null,
                    TargetEffect = null,
                    SpellDistance = 4f,
                    CD = 5f,
                    SkillName = "W技能",
                    IconPath = "00041",
                    LongDescription = "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化," +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化" +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化" +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化",
                    SkillLevel = 6
                },
                new PointingSkill{
                    BaseDamage = 1000,
                    KeyCode = KeyCode.R,
                    Mp = 220,
                    PlusDamage = 200,
                    SelfEffect = null,
                    TargetEffect = null,
                    SpellDistance = 4f,
                    CD = 5f,
                    SkillName = "W技能",
                    IconPath = "00041",
                    LongDescription = "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化," +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化" +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化" +
                    "用于测试，这是一个技能描述，比较长的测试，用来观察富文本框的长度会产生怎样的变化",
                    SkillLevel = 6
                }
            },
            passiveSkills = new List<PassiveSkill> {
            }
        };
        Owner = new Player() {
            Money = 1000
        };
    }
    //================================================
    #endregion

    public void Awake() {
        if (CompareTag("Player"))
            Install();
        //if (CompareTag("Enermy"))
        //    wayPointsUnit = new WayPointsUnit(WayPointEnum.UpRoad,UnitFaction.Red);

        characterModel.Hp = characterModel.maxHp;
        characterModel.Mp = characterModel.maxMp;

        // 获得该单位身上绑定的组件
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        // 获得该单位周围的敌人
        arroundEnemies = transform.Find("SearchTrigger").GetComponent<SearchTrigger>().arroundEnemies;

        // 初始化六个物品格子,六个物品格子在物品栏中的摆放顺序是
        // 从上到下,从左到右,依次顺序,123456
        characterModel.itemGrids = new List<ItemGrid> {
            new ItemGrid{ item=null,ItemCount=0,hotKey=KeyCode.Alpha1,index=1 },
            new ItemGrid{ item=null,ItemCount=0,hotKey=KeyCode.Alpha2,index=2 },
            new ItemGrid{ item=null,ItemCount=0,hotKey=KeyCode.Alpha3,index=3 },
            new ItemGrid{ item=null,ItemCount=0,hotKey=KeyCode.Alpha4,index=4 },
            new ItemGrid{ item=null,ItemCount=0,hotKey=KeyCode.Alpha5,index=5 },
            new ItemGrid{ item=null,ItemCount=0,hotKey=KeyCode.Alpha6,index=6 },
        };

        //============================
        // 与ViewModel双向绑定
        Bind();

        #region 对所有单位的测试
        //================================================
        // 测试
        if (CompareTag("Player")) {
            characterModel.itemGrids[0].item = new Item {
                name = "测试物品",
                itemActiveSkill = new PointingSkill {
                    BaseDamage = 1000,
                    SelfEffect = targetPositionEffect,
                    TargetEffect = targetPositionEffect,
                    SpellDistance = 10,
                    CD = 3
                },
                itemType = ItemType.Consumed,
                maxCount = 10,
                iconPath = "00046",
                useMethodDescription = "使用：点目标",
                activeDescription = "对一个目标进行投掷，造成伤害对一个目标进行投掷，造成伤害对一个目标进行投掷，造成伤害对一个目标进行投掷，造成伤害对一个目标进行投掷，造成伤害对一个目标进行投掷，造成伤害对一个目标进行投掷，造成伤害对一个目标进行投掷，造成伤害",
                passiveDescription = "+100攻击力\n+100防御力\n+10力量",
                backgroundDescription = "一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品一个用来测试的物品"
            };
            characterModel.itemGrids[0].ItemCount = 3;

            characterModel.itemGrids[1].item = new Item {
                name = "测试物品",
                itemActiveSkill = new PointingSkill {
                    BaseDamage = 1000,
                    SelfEffect = targetPositionEffect,
                    TargetEffect = targetPositionEffect,
                    SpellDistance = 10,
                    CD = 3
                },
                itemType = ItemType.Consumed,
                maxCount = 10,
                iconPath = "00046",
                useMethodDescription = "使用：点目标",
                activeDescription = "对一个目标进行投掷",
                passiveDescription = "+100攻击力\n+100防御力\n+10力量",
                backgroundDescription = "一个用来测试的物品"
            };
            characterModel.itemGrids[1].ItemCount = 5;
        }

        HaloSkill haloSkill = new HaloSkill() { SkillLevel = 1, inflenceRadius = 10, targetFaction = UnitFaction.Red };
        haloSkill.Execute(this);
        #endregion
    }

    public virtual void Update() {

        // 处理单位的状态
        for (int i = 0; i < battleStates.Count;) {
            BattleState battleState = battleStates[i];

            // 更新状态
            battleState.Update(this);

            // 如果该状态没有消失，去更新下一个状态
            // 如果该状态消失了，那么i不进行++
            if (!battleState.IsStateDying) {
                i++;
            }

        }

    }

    #region 人物的逻辑操作,包括 追逐敌人、攻击敌人、施法、移动等操作
    //=====================================================
    // 人物的逻辑操作,包括 追逐敌人、攻击敌人、施法、移动等操作

    /// <summary>
    /// 处理人物追击的逻辑
    /// 当人物追击完成(也就是移动到了目标单位面前)返回true,否则返回false 
    /// <para></para>
    /// 追击部分
    /// 当移动到小于攻击距离时，自动停止移动,
    /// 否则继续移动,直到追上敌人,或者敌人消失在视野中
    /// </summary>
    /// <param name="targetTransform">要追击的单位的位置</param>
    /// /// <param name="forwardDistance">跟目标的距离</param>
    /// <returns></returns>
    public bool Chasing(Vector3 position,float forwardDistance) {

        // 获得当前单位与目标单位的距离
        float distance = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(position.x, position.z)
        );

        if (!agent.pathPending && distance <= forwardDistance) {
            animator.SetBool("isRun", false);
            agent.isStopped = true;
            return true;
        } else {
            animator.SetBool("isRun", true);
            agent.isStopped = false;
            agent.SetDestination(position);
            return false;
        }

    }

    /// <summary>
    /// 判断目标是否处于当前单位的正面
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public bool IsTargetFront(CharacterMono target) {
        // 获得从当前单位到目标单位的方向向量
        Vector3 direction = target.transform.position - transform.position;
        if (Vector3.Angle(transform.forward, direction) <= 30f)
            return true;
        else
            return false;
    }

    /// <summary>
    /// 处理人物攻击的函数,完成一次攻击返回True，否则返回False
    /// </summary>
    /// <param name="isAttackFinish">本次攻击是否完成</param>
    /// <param name="targetTransform">目标敌人的Transform</param>
    /// <param name="target">目标敌人的Mono对象</param>
    public virtual bool Attack(ref bool isAttackFinish, Transform targetTransform, CharacterMono target) {

        if (!target.IsCanBeAttack()) {
            ResetAttackStateAnimator();
            arroundEnemies.Remove(target);
            return false;
        }

        AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextAnimatorStateInfo = animator.GetNextAnimatorStateInfo(0);

        // 判断单位是否正对目标，如果没有，则转身面对目标在进行攻击(注意必须是在单位没有攻击时，才转向敌人)
        if (!IsTargetFront(target) && !currentAnimatorStateInfo.IsName("attack")) {

            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(target.transform.position - transform.position), characterModel.turningSpeed * Time.deltaTime);

            return false;
        }

        //======================================
        // 播放攻击动画
        // 如果准备开始攻击,那么播放动画
        if (!currentAnimatorStateInfo.IsName("attack")) {
            animator.SetTrigger("attack");
            isAttackFinish = false;
        }


        //======================================
        // 伤害判断
        if (currentAnimatorStateInfo.IsName("attack") &&
            nextAnimatorStateInfo.IsName("Idle") &&
            !isAttackFinish) {

            if (characterModel.projectileModel == null) {
                Damage damage = new Damage(characterModel.TotalAttack,0);

                // 执行所有倍增伤害的被动技能
                foreach (PassiveSkill passiveSkill in characterModel.passiveSkills) {
                    if (passiveSkill.TiggerType == PassiveSkillTriggerType.WhenAttack || passiveSkill.TiggerType == PassiveSkillTriggerType.WhenNormalAttack) {
                        passiveSkill.Execute(this,target,ref damage);
                    }
                }

                target.characterModel.Damaged(damage,this);

                #region 测试，使敌方进入中毒状态
                // 测试，使敌方进入中毒状态
                target.AddBattleState(new PoisoningState{
                    damage = new Damage(40, 10),
                    duration = 15f,
                    stateHolderEffect = stateHolderEffect,
                    name = "PosioningState",
                    iconPath = "00046",
                    description = "中毒技能,每秒中减少20点生命值",
                });
                #endregion

                // 近战攻击事件，向所有订阅近战攻击的观察者发送消息
                if (OnAttack != null) OnAttack(this,target,damage);

            } else {
                Transform shotPosition = transform.Find("shootPosition");
                ProjectileMono projectileMono = Instantiate(characterModel.projectile, shotPosition.position, Quaternion.identity);
                projectileMono.targetPosition = targetTransform.position;
                projectileMono.target = target;
                projectileMono.damage = new Damage() { BaseDamage = 300,PlusDamage=300 };

                // 执行所有倍增伤害的被动技能
                foreach (PassiveSkill passiveSkill in characterModel.passiveSkills) {
                    if (passiveSkill.TiggerType == PassiveSkillTriggerType.WhenAttack || passiveSkill.TiggerType == PassiveSkillTriggerType.WhenNormalAttack) {
                        passiveSkill.Execute(this, target, ref projectileMono.damage);
                    }
                }

                projectileMono.projectileModel = characterModel.projectileModel;
            }
            isAttackFinish = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 重置人物的攻击动画
    /// </summary>
    public void ResetAttackStateAnimator() {
        animator.ResetTrigger("attack");
    }

    /// <summary>
    /// 重置人物的施法动画
    /// </summary>
    public void ResetSpeellStateAnimator() {
        animator.ResetTrigger("spell");
    }

    /// <summary>
    /// 重置目前单位的所有动画,如攻击动画、移动动画、施法动画。
    /// </summary>
    public void ResetAllStateAnimator() {
        animator.ResetTrigger("spell");
        animator.ResetTrigger("attack");
        animator.SetBool("isRun", false);
    }

    /// <summary>
    /// 单位回到Idle状态时进行的处理
    /// </summary>
    public void ResetIdle() {
        // 清空动画状态
        ResetAllStateAnimator();
        // 设置agent为不可行动
        agent.isStopped = true; 
    }

    /// <summary>
    /// 移动到指定地点,移动结束返回False,移动尚未结束返回True
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public bool Move(Vector3 position) {
        ResetAttackStateAnimator();
        animator.SetBool("isRun", true);
        agent.isStopped = false;
        agent.SetDestination(position);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance) {
            animator.SetBool("isRun", false);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断人物当前准备释放的技能是不是立即释放技能,如果是,那么返回True,反之返回False
    /// </summary>
    /// <returns></returns>
    public bool IsImmediatelySpell() {
        return prepareSkill.SpellDistance == 0;
    }
     
    /// <summary>
    /// 适用于指定敌人的施法技能
    /// 释放技能的函数,施法结束返回True,施法失败或施法未完成返回False
    /// </summary>
    public bool Spell(CharacterMono enemryMono,Vector3 position) {

        // 如果目标已经不可攻击,那么返回False
        if (enemryMono!=null && !enemryMono.IsCanBeAttack()) {
            ResetSpeellStateAnimator();
            arroundEnemies.Remove(enemryMono);

            return false;
        }

        // 获得当前动画和下一个动画状态
        AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextAnimatorStateInfo = animator.GetNextAnimatorStateInfo(0);

        if (IsImmediatelySpell()) {
            // 原地释放技能,此时直接释放技能

            // 播放释放技能的动画
            if (!currentAnimatorStateInfo.IsName("Spell"))
                animator.SetTrigger("spell");

            // 如果技能释放结束,那么产生特效,计算伤害
            if (currentAnimatorStateInfo.IsName("Spell") &&
                nextAnimatorStateInfo.IsName("Idle")) {

                if (!isPrepareUseItemSkill)
                    prepareSkill.Execute(this, enemryMono);
                else {
                    prepareItemSkillItemGrid.ExecuteItemSkill(this, enemryMono);
                    isPrepareUseItemSkill = false;
                    prepareItemSkillItemGrid = null;
                }

                isPrepareUseSkill = false;
                prepareSkill = null;
                return true;
            }
        } else {
            // 指向型技能

            // 当前距离敌人 > 施法距离,进行追击
            if (Chasing(position,prepareSkill.SpellDistance)) {
                //======================================
                // 播放施法动画
                // 如果准备开始施法,那么播放动画
                if (!currentAnimatorStateInfo.IsName("Spell")) {
                    animator.SetTrigger("spell");
                }

                // 如果技能释放结束,那么产生特效,计算伤害
                if (currentAnimatorStateInfo.IsName("Spell") &&
                    nextAnimatorStateInfo.IsName("Idle")) {

                    if (!isPrepareUseItemSkill)
                        if(prepareSkill.IsMustDesignation)
                            prepareSkill.Execute(this, enemryMono);
                        else
                            prepareSkill.Execute(this, position);
                    else {
                        prepareItemSkillItemGrid.ExecuteItemSkill(this,enemryMono);
                        isPrepareUseItemSkill = false;
                        prepareItemSkillItemGrid = null;
                    }


                    isPrepareUseSkill = false;
                    prepareSkill = null;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 适用于指定地点的施法技能
    /// 释放技能的函数,施法结束返回True,施法失败或施法未完成返回False
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public bool Spell(Vector3 position) {
        return Spell(null,position);
    }

    /// <summary>
    /// 当单位受到伤害时执行的事件
    /// </summary>
    /// <param name="damage"></param>
    /// <param name="attacker"></param>
    private void OnDamged(Damage damage, CharacterMono attacker, int nowHp) {

        //=======================================================
        // 处理单位死亡时,敌对单位获得经验的行为
        if (attacker!=null && !this.isDying && !attacker.CompareOwner(this) && nowHp==0) {
            // 在攻击者的目标位置以r为半径的区域内,所有跟攻击者一个阵营的单位获得经验
            Collider[] colliders = Physics.OverlapSphere(attacker.transform.position,5);
            foreach (var collider in colliders) {
                HeroMono characterMono = collider.GetComponent<HeroMono>();
                if (characterMono != null && characterMono.CompareOwner(attacker)) {
                    characterMono.HeroModel.Exp += this.characterModel.supportExp;
                    Debug.Log("单位："+characterMono.name+"的经验目前是："+characterMono.HeroModel.Exp);
                }
            }
        }
    }

    #region 单位的死亡逻辑 hp=0 -> OnDying -> Dying -> Died -> IsDied -> Destory(this)
    /// <summary>
    /// 单位进入垂死状态
    /// </summary>
    /// <returns></returns>
    private void Dying() {

        // 设置isDying为True
        isDying = true;

        // 停止目前一切动作
        ResetAllStateAnimator();

        // 把人物的AI系统暂停
        BehaviorTree behaviorTree = GetComponent<BehaviorTree>();
        if (behaviorTree != null)
            behaviorTree.enabled = false;

        CharacterOperationFSM characterOperationFSM = GetComponent<CharacterOperationFSM>();
        if (characterOperationFSM != null)
            characterOperationFSM.enabled = false;

        // 播放死亡动画
        animator.SetTrigger(AnimatorEnumeration.Died);
    }

    /// <summary>
    /// 判断单位是否确确实实死了
    /// <para>确确实实死亡指的是目标单位的死亡动画已经播放完毕了</para>
    /// </summary>
    /// <returns></returns>
    private bool IsDied() {
        AnimatorStateInfo currentAnimatorStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo nextAnimatorStateInfo = animator.GetNextAnimatorStateInfo(0);

        // 当死亡动画播放完毕,单位确实死了
        if (isDying && currentAnimatorStateInfo.IsName("Death") && nextAnimatorStateInfo.IsName("Idle")) {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 与CharacterModel的Hp属性绑定的方法,当Hp为0时,宣告单位死亡
    /// </summary>
    /// <param name="oldHp"></param>
    /// <param name="newHp"></param>
    private void OnDying(int oldHp,int newHp) {
        if (newHp == 0) {
            // 单位进入垂死状态,执行相关操作,如暂停行为树的执行
            Dying();

            // 开启单位死后善后的协程
            StartCoroutine(Died());
        }
    }
    
    /// <summary>
    /// 人物死亡进行的操作,每帧判断一次,当人物死亡动画播放完毕时,摧毁该单位
    /// </summary>
    /// <returns></returns>
    public IEnumerator Died() {

        while (isDying) {
            if (IsDied()) {
                Destroy(gameObject);
                isDying = false;
            }
            yield return new WaitForFixedUpdate();
        }
    }

    /// <summary>
    /// 判断单位是否可以被攻击,
    /// 不可以被攻击的单位可能:
    ///     1.垂死的
    ///     2.已被摧毁的
    ///     3.已死亡的
    ///     4.无敌的
    ///     ...待续
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public bool IsCanBeAttack() {

        CharacterMono target = this;

        // 如果单位被摧毁,那么目标单位无法被攻击
        if (target == null || !target.enabled) return false;


        // 垂死单位不可被攻击
        if (target.isDying) return false;

        // 无敌的单位不可被攻击
        if (!target.characterModel.canBeAttacked) return false;

        return true;
    }

    #endregion

    /// <summary>
    /// 判断目标和自己是否同属一个阵营,当目标为中立单位时,同样返回True
    /// </summary>
    public bool CompareOwner(CharacterMono target) {
        if (characterModel.unitFaction == target.characterModel.unitFaction || target.characterModel.unitFaction == UnitFaction.Neutral)
            return true;
        else
            return false;
    }

    #endregion

    #region 绑定UI，这份代码需要重构，因为UI和人物耦合了
    //======================================
    // ●绑定Model中的各项属性到ViewModel中
    protected virtual void Bind() {
        characterModel.HpValueChangedHandler += OnDying;        // 绑定监测死亡的函数
        characterModel.HpValueChangedHandler += OnHpValueChanged;
        characterModel.OnDamaged += OnDamged; 

        //==========================
        // 绑定物品
        foreach (var itemGrid in characterModel.itemGrids) {
            itemGrid.OnIconPathChanged += OnItemIconPathChanged;
            itemGrid.OnItemCountChanged += OnItemCountChanged;
        }


    }
    public void OnHpValueChanged(int oldHp,int newHp) {
        if(simpleCharacterViewModel!=null)
            simpleCharacterViewModel.Hp.Value = newHp;
    }
    public void OnItemCountChanged(int oldItemCount,int newItemCount,int index) {
        if(itemViewModels.Count>=index)
            ItemViewModels[index - 1].itemCount.Value = newItemCount; 
    }
    public void OnItemIconPathChanged(string oldItemPath,string newItemPath,int index) {
        if (itemViewModels.Count >= index)
            ItemViewModels[index - 1].iconPath.Value = newItemPath;
    }
    #endregion
}