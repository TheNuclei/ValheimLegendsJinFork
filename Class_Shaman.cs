﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

namespace ValheimLegends
{
    public class Class_Shaman
    {
        private static int Script_Layermask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece_nonsolid", "terrain", "vehicle", "piece", "viewblock");
        private static int ObjectBlock_Layermask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece_nonsolid", "terrain", "vehicle", "piece", "viewblock", "item");

        public static bool isWaterWalking = false;
        private static int glideDelay = 0;
        public static float shell_spiritdamage_base = 10f;
        public static float shell_spiritdamage_scaling = .5f;
        public static float shell_resistModifier_base = .6f;
        public static float shell_resistModifier_negscaling = .6f;
        public static float enrage_staminamodifier_base = 5f;
        public static float enrage_staminamodifier_scaling = .1f;
        public static float enrage_speedmodifier_base = 1.2f;
        public static float enrage_speedmodifier_scaling = .0025f;

        public static void Process_Input(Player player, ref Rigidbody playerBody, ref float altitude, ref float lastGroundTouch, float waterLevel)
        {
            ValheimLegends.isChanneling = false;
            if (ZInput.GetButton("Jump"))
            {
                glideDelay++;
                if (!player.IsDead() && !player.InAttack() && !player.IsEncumbered() && !player.InDodge() && !player.IsKnockedBack() && glideDelay > 20)
                {
                    if (player.transform.position.y <= (waterLevel + .4f))
                    {
                        bool flag = true;
                        if (!player.HaveStamina(1f))
                        {
                            if (player.IsPlayer())
                            {
                                Hud.instance.StaminaBarEmptyFlash();
                            }
                            flag = false;
                            isWaterWalking = false;
                        }
                        if (flag)
                        {
                            //isWaterWalking = true;
                            player.UseStamina(.3f * VL_GlobalConfigs.c_shamanBonusWaterGlideCost);
                            VL_Utility.RotatePlayerToTarget(player);
                            ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).StopAllCoroutines();
                            RaycastHit hitInfo = default(RaycastHit);
                            Vector3 position = player.transform.position + (player.transform.up * .15f);
                            Vector3 vector = player.GetLookDir();
                            vector.y = 0f;
                            Physics.SphereCast(position, .1f, vector, out hitInfo, 10f, ObjectBlock_Layermask);
                            //Vector3 target = (!Physics.Raycast(position, player.GetLookDir(), out hitInfo, float.PositiveInfinity, ObjectBlock_Layermask) || !(bool)hitInfo.collider) ? (position + player.GetLookDir() * 1000f) : hitInfo.point;
                            Vector3 newPos = new Vector3(player.transform.position.x + (player.GetLookDir().x * .3f), waterLevel + .3f, player.transform.position.z + (player.GetLookDir().z * .3f));
                            if ((Vector3.Distance(position, newPos)+.25f) > (Vector3.Distance(position, hitInfo.point)))
                            {
                                newPos = new Vector3(player.transform.position.x, waterLevel + .3f, player.transform.position.z);
                            }
                            playerBody.position = newPos;
                            playerBody.velocity = Vector3.zero;
                            ValheimLegends.isChanneling = true;
                            //ZLog.Log("player position " + player.transform.position + " water level " + waterLevel + " new pos " + newPos + " velocity " + playerBody.velocity);
                            UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_VL_FlyingKick"), player.transform.position + player.transform.up * -.2f, Quaternion.LookRotation(player.transform.forward * -1f));
                        }
                    }
                }
            }
            else
            {
                glideDelay = 0;
            }

            if(player.transform.position.y +.3f > waterLevel)
            {
                isWaterWalking = false;
            }

            if (VL_Utility.Ability3_Input_Down)
            {
                //player.Message(MessageHud.MessageType.Center, "Spirit Bomb"); //deals moderate spirit damage in PBAoE and applies spirit dot
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability3_CD".GetStableHashCode()))
                {
                    if (player.GetStamina() > VL_Utility.GetSpiritBombCost(player))
                    {
                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability3_CD)ScriptableObject.CreateInstance(typeof(SE_Ability3_CD));
                        se_cd.m_ttl = VL_Utility.GetSpiritBombCooldown(player);
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetSpiritBombCost(player));

                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.EvocationSkillDef).m_level;

                        //Effects, animations, and sounds
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("battleaxe_attack1");
                        UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_goblinking_nova"), player.transform.position, Quaternion.identity);

                        //Lingering effects
                        SE_SpiritDrain se_spiritdrain = (SE_SpiritDrain)ScriptableObject.CreateInstance(typeof(SE_SpiritDrain));
                        se_spiritdrain.m_ttl = SE_SpiritDrain.m_baseTTL;
                        se_spiritdrain.damageModifier = 1f + (.1f * sLevel);

                        //Apply effects
                        List<Character> allCharacters = Character.GetAllCharacters();
                        foreach (Character ch in allCharacters)
                        {
                            if ((BaseAI.IsEnemy(player, ch) && (ch.transform.position - player.transform.position).magnitude <= 11f + (.05f * sLevel)) && VL_Utility.LOS_IsValid(ch, player.GetCenterPoint(), player.transform.position))
                            {
                                Vector3 direction = (ch.transform.position - player.transform.position);
                                HitData hitData = new HitData();
                                hitData.m_damage.m_spirit = UnityEngine.Random.Range(6f + (.4f * sLevel), 12f + (.6f * sLevel)) * VL_GlobalConfigs.g_DamageModifer * VL_GlobalConfigs.c_shamanSpiritShock;
                                hitData.m_damage.m_lightning = UnityEngine.Random.Range(6f + (.4f * sLevel), 12f + (.6f * sLevel)) * VL_GlobalConfigs.g_DamageModifer * VL_GlobalConfigs.c_shamanSpiritShock;
                                hitData.m_pushForce = 25f + (.1f * sLevel);
                                hitData.m_point = ch.GetEyePoint();
                                hitData.m_dir = direction;
                                hitData.m_skill = ValheimLegends.EvocationSkill;
                                ch.Damage(hitData);
                                ch.GetSEMan().AddStatusEffect(se_spiritdrain);
                            }
                        }

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.EvocationSkill, VL_Utility.GetSpiritBombSkillGain(player));
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina for Spirit Shock: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetSpiritBombCost(player) + ")");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
            else if(VL_Utility.Ability2_Input_Down)
            {                
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability2_CD".GetStableHashCode()))
                {
                    if (player.GetStamina() > VL_Utility.GetShellCost(player))
                    {
                        //player.Message(MessageHud.MessageType.Center, "Shell"); //add elemental resistances and spirt damage to attacks
                        //Ability Cooldown                        
                        StatusEffect se_cd = (SE_Ability2_CD)ScriptableObject.CreateInstance(typeof(SE_Ability2_CD));
                        se_cd.m_ttl = VL_Utility.GetShellCooldown(player);
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetShellCost(player));

                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.AbjurationSkillDef).m_level;

                        //Effects, animations, and sounds
                        ValheimLegends.shouldUseGuardianPower = false;
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("gpower");
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetSpeed(1.25f);
                        //player.StartEmote("cheer");

                        //Lingering effects
                        
                        List<Character> allCharacters = new List<Character>();
                        allCharacters.Clear();
                        Character.GetCharactersInRange(player.transform.position, (30f + .2f * sLevel), allCharacters);
                        GameObject effect = ZNetScene.instance.GetPrefab("fx_guardstone_permitted_add");
                        foreach (Character p in allCharacters)
                        {
                            SE_Shell se_shell = (SE_Shell)ScriptableObject.CreateInstance(typeof(SE_Shell));
                            se_shell.m_ttl = SE_Shell.m_baseTTL + (.3f * sLevel);
                            se_shell.spiritDamageOffset = (shell_spiritdamage_base + (shell_spiritdamage_scaling * sLevel)) * VL_GlobalConfigs.g_DamageModifer * VL_GlobalConfigs.c_shamanShell;
                            se_shell.resistModifier = (.6f - (.006f * sLevel)) * VL_GlobalConfigs.c_shamanShell;
                            se_shell.m_icon = ZNetScene.instance.GetPrefab("ShieldSerpentscale").GetComponent<ItemDrop>().m_itemData.GetIcon();
                            se_shell.m_tooltip = $"Reduces elemental damage taken by {(int)((1f - se_shell.resistModifier) * 100f)}%, while adding {(int)(se_shell.spiritDamageOffset)} Spirit damage to your attacks";
                            se_shell.doOnce = false;
                            if (!BaseAI.IsEnemy(player, p))
                            {
                                if (p == Player.m_localPlayer)
                                {
                                    p.GetSEMan().AddStatusEffect(se_shell, true);
                                }
                                else if (p.IsPlayer())
                                {
                                    p.GetSEMan().AddStatusEffect(se_shell.name.GetStableHashCode(), true);
                                }
                                else
                                {
                                    p.GetSEMan().AddStatusEffect(se_shell, true);
                                }
                                UnityEngine.Object.Instantiate(effect, p.GetCenterPoint(), Quaternion.identity);
                            }                         
                        }

                        //Apply effects
                        UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("sfx_wraith_death"), player.transform.position, Quaternion.identity);

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.AbjurationSkill, VL_Utility.GetShellSkillGain(player));
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina for Shell: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetShellCost(player) + ")");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
            else if (VL_Utility.Ability1_Input_Down)
            {
                //add movement speed, stamina regeneration
                if (!player.GetSEMan().HaveStatusEffect("SE_VL_Ability1_CD".GetStableHashCode()))
                {
                    if (player.GetStamina() > VL_Utility.GetEnrageCost(player))
                    {
                        //player.Message(MessageHud.MessageType.Center, "Enrage");
                        //Ability Cooldown
                        StatusEffect se_cd = (SE_Ability1_CD)ScriptableObject.CreateInstance(typeof(SE_Ability1_CD));
                        se_cd.m_ttl = VL_Utility.GetEnrageCooldown(player);
                        player.GetSEMan().AddStatusEffect(se_cd);

                        //Ability Cost
                        player.UseStamina(VL_Utility.GetEnrageCost(player));

                        //Skill influence
                        float sLevel = player.GetSkills().GetSkillList().FirstOrDefault((Skills.Skill x) => x.m_info == ValheimLegends.AlterationSkillDef).m_level;

                        //Effects, animations, and sounds
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Player.m_localPlayer)).SetTrigger("challenge");
                        GameObject effectPlayer = ZNetScene.instance.GetPrefab("fx_guardstone_permitted_removed");
                        effectPlayer.transform.localScale = Vector3.one * 3f;
                        UnityEngine.Object.Instantiate(effectPlayer, player.GetCenterPoint(), Quaternion.identity);
                        //UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("sfx_wraith_death"), player.transform.position, Quaternion.identity);

                        //Lingering effects

                        //if, else added
                        //ObjectDB.instance.GetStatusEffect(se.name).m_ttl += 115; added

                        //Apply effects
                        GameObject effectApplied = ZNetScene.instance.GetPrefab("fx_GP_Activation");
                        List<Character> allCharacters = new List<Character>();
                        Character.GetCharactersInRange(player.transform.position, 30f, allCharacters);
                        SE_Enrage se_enrage = (SE_Enrage)ScriptableObject.CreateInstance(typeof(SE_Enrage));
                        se_enrage.m_ttl = 16f + (.2f * sLevel);
                        se_enrage.staminaModifier = (enrage_staminamodifier_base + (enrage_staminamodifier_scaling * sLevel)) * VL_GlobalConfigs.g_DamageModifer * VL_GlobalConfigs.c_shamanEnrage;
                        se_enrage.speedModifier = enrage_speedmodifier_base + (enrage_speedmodifier_scaling * sLevel); 
                        se_enrage.m_icon = ZNetScene.instance.GetPrefab("TrophyGoblinBrute").GetComponent<ItemDrop>().m_itemData.GetIcon();
                        se_enrage.doOnce = false;
                        se_enrage.m_tooltip = $"Enraged by a shaman, regenerating {se_enrage.staminaModifier} stamina per second and moving {(int)((se_enrage.speedModifier - 1f) * 100f)}% faster";

                        foreach (Character p in allCharacters)
                        {
                            if (!BaseAI.IsEnemy(player, p))
                            {
                                if (p == Player.m_localPlayer)
                                {
                                    p.GetSEMan().AddStatusEffect(se_enrage, true);
                                }
                                else if (p.IsPlayer())
                                {
                                    p.GetSEMan().AddStatusEffect(se_enrage.name.GetStableHashCode(), true);
                                }
                                else
                                {
                                    p.GetSEMan().AddStatusEffect(se_enrage, true);
                                }
                                UnityEngine.Object.Instantiate(effectApplied, p.GetCenterPoint(), Quaternion.identity);
                            }
                        }

                        //Skill gain
                        player.RaiseSkill(ValheimLegends.AlterationSkill, VL_Utility.GetEnrageSkillGain(player));
                    }
                    else
                    {
                        player.Message(MessageHud.MessageType.TopLeft, "Not enough stamina for Enrage: (" + player.GetStamina().ToString("#.#") + "/" + VL_Utility.GetEnrageCost(player) + ")");
                    }
                }
                else
                {
                    player.Message(MessageHud.MessageType.TopLeft, "Ability not ready");
                }
            }
        }
    }
}
