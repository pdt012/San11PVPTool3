/*{
author=氕氘氚
version=1.0
date=2026/3/20
}*/
/***
联机扩展功能（过回合自动存档等）
配合三国志11联机工具使用

## 一、自动存档时机
1. 回合结束时（无论玩家或AI），判断下一个行动势力，如果是玩家，则随机选取一个贼势力插入到玩家势力前，并改为玩家；
   * 在onNewDay时，判断第一个势力是否为正常玩家。如是，则执行同样的加入贼势力并将其改为玩家的逻辑；
2. 在'贼势力玩家'回合结束时，自动存档。确保自动存档的下一个势力，必定是下一个应该行动的玩家；
3. 存档之前把下一个行动的玩家势力存入共享文件；
4. 正常玩家过回合时，再将'贼势力玩家'移回turn table原位，并改回为AI;
5. 理论上，当把'贼势力'变为玩家时，不会对贼势力有操作；当把'贼势力'移回原位并改回AI后，贼势力还会自动按正常AI逻辑操作。

## 二、自动读档时机
1. 当收到联机工具通知，当前行动势力等于当前玩家时，自动读档；
2. 如果读出的存档，当前保存的势力为'贼势力玩家'时，游戏会自动过该AI回合，进入当前玩家回合，无需特别处理；
3. 如果读出的存档，如果当前保存的势力为玩家时，理论上该玩家必定不是当前玩家。则cpp需自动结束该玩家回合，进入当前玩家回合。

## 三、举例说明
* 新开游戏时行动顺序：电脑1 -> 电脑2 -> 玩家1 -> 电脑3 -> 玩家2 -> 玩家3 -> 电脑4 -> ... -> 贼1 -> 贼2 -> ...
* 首先到玩家1回合，玩家1过回合后，在电脑3过回合时：
  ** 行动顺序变为：电脑1 -> 电脑2 -> 玩家1 -> 电脑3 -> 贼1(玩家) -> 玩家2 -> 玩家3 -> 电脑4 -> ... -> 贼2 -> ...
  ** 玩家1停留在'贼1(玩家)'，禁过回合、存档、读档等操作，只能看地图及联机信息；
  ** 此时自动存档，存档的当前势力为贼1(玩家)；
* 联机工具自动上传存档，并在玩家2电脑上自动下载存档，并写入共享文件
* 玩家2电脑读取到共享文件的通知，自动读档，自动过掉贼1(玩家)的回合，进入自己的回合

## 四、[不需要了] 修改turn_table顺序
   原版在每回合开始OnNewDay时，会重新计算本回合的turn_table，逻辑为"城市数-武将数-港关数"，越少越优先
   这种方式在联机时会有两个问题：
   1. [此问题已通过'贼势力玩家'解决] 玩家势力为本回合第一个行动势力时，无法在此玩家行动前触发自动存档；
   2. 存在"二动"问题
   因此修改turn_table为按照势力id固定顺序
*/

namespace 联机扩展
{
    bool 调试模式 = false;
    int turn_table_type = 1; // 0: 原版顺序(改); 1: 固定顺序
    DlgPVPInfo@ g_dlg_pvp_info = null;
    bool g_auto_skip_turn = false;
    array<int> g_arr_bandit_force_id = {势力_羌, 势力_山越, 势力_乌丸, 势力_南蛮, 势力_盗贼};

    class Main
    {
        int priority = 101;
        int player_count = 0;
        private bool _is_just_loaded = false;
        // 是否是新开游戏首回合首个玩家，用于控制**不在**首回合首个玩家前插入贼势力
        private bool _if_need_insert_bandit = false;
        private int _dlg73_timer = 0;
        array<int> arr_ally_force;

        Main()
        {
            /*
            1. 102在103之前执行
            2. 重新开始新游戏时，102中pk::get_scenario().loaded为false
            3. 读档时，103不触发，先触发读档，再触发102。102中pk::get_scenario().loaded为true
            */

            // 联机模式时创建联机信息框，(尚未实现)禁用存档、读档、快速存档、快速读档等
            pk::bind(102, pk::trigger102_t(onGameInit));

            // onLoad时计算player_count，以判断当前是否为联机模式，未来应该不使用player_count判断
            pk::bind(106, pk::trigger106_t(onLoad));

            // onNewDay时重设turn_table
            pk::bind(107, pk::trigger107_t(onNewDay));

            // onTurnStart时隐藏联机信息框，激活操作
            pk::bind(111, priority, pk::trigger111_t(onTurnStart));

            // onTurnEnd时处理自动保存的逻辑，显示联机信息框并禁操作
            pk::bind(112, pk::trigger112_t(onTurnEnd));

            // 打开message box时触发，用于自动跳过贼势力玩家的msgbox
            pk::bind(227, pk::trigger227_t(onPersonSetMsgBox));
        }

        void onGameInit()
        {
            // if (pk::get_scenario().loaded == false)
            // {
            // 新开游戏，非读档情况
            this.player_count = get_player_count();
            //     pk::trace("联机扩展onGameInit...读取player_count");
            // }

            int force_id = pk::get_current_turn_force_id();
            pk::force@ force = pk::get_force(force_id);

            pk::trace("联机扩展onGameInit...当前势力: " + pk::decode(pk::get_name(force)) + ", " + (force.is_player() ? "***玩家***" : "电脑"));
            if (player_count < 2)
                return;

            // 🔵INFO: 读档时会销毁g_dlg_pvp_info.main_dlg，因此在这里新建
            if (g_dlg_pvp_info is null)
            {
                @g_dlg_pvp_info = DlgPVPInfo();
                g_dlg_pvp_info.create();
                // pk::detail::funcref func = pk::widget_on_destroy_t(_on_destory_dlg_pvp_info);
                // g_dlg_pvp_info.on_widget_destory = func;
                // g_dlg_pvp_info.main_dlg.on_widget_destroy(func);
                g_dlg_pvp_info.set_visible(false);
            }
            // else
            // {
            if (pk::get_scenario().loaded)
            {
                // 读取玩存档的首回合，必定是设为玩家的贼势力
                int next_force_id = get_next_force_id_in_same_turn();
                if (pk::is_valid_force_id(next_force_id))
                {
                    pk::force@ next_force = pk::get_force(next_force_id);
                    g_dlg_pvp_info.set_action_player(pk::get_name(next_force));
                }
                else
                {
                    g_dlg_pvp_info.set_action_player("");
                }

                g_dlg_pvp_info.set_visible(true);
            }
            else
            {
            }
            // }

            // if (_is_just_loaded)
            // {
            // pk::next_turn();
            // pk::trace("联机扩展...pk::next_turn() EXECUTED");
            // }

            pk::trace("联机扩展onGameInit...END");
        } // onGameInit

        void onLoad(int file_id, pk::s11reader@ r)
        {
            pk::trace("联机扩展onLoad...START");
            _is_just_loaded = true;
            _if_need_insert_bandit = true; // 联机模式读档时，必定置为true
            g_auto_skip_turn = true;
            this.arr_ally_force.resize(0);

            int force_id = pk::get_current_turn_force_id();
            pk::force@ force = pk::get_force(force_id);

            pk::trace("======== ch::get_set_p(0).get_mod_set(信息迷雾系统_开关): " + ch::get_set_p(0).get_mod_set(信息迷雾系统_开关));

            pk::trace("联机扩展onLoad...END, 已读取势力: " + pk::decode(pk::get_name(force)) + ", " + (force.is_player() ? "***玩家***" : "电脑"));
            // this.player_count = get_player_count();
        }

        void onNewDay()
        {
            pk::trace("联机扩展onNewDay...START");
            if (player_count < 2)
                return;

            // reset_turn_table(turn_table_type);

            pk::scenario@ scenario = pk::get_scenario();
            // 如果是开局第一回合，不需要移动贼势力
            if (scenario.turn_counter != 0)
            {
                // 检查第一个势力是否为玩家
                int first_force_id = scenario.get_turn_table_force_id(0);
                if (pk::is_valid_force_id(first_force_id))
                {
                    pk::force@ first_force = pk::get_force(first_force_id);
                    if (first_force.is_player())
                    {
                        int bandit_force_id = find_suitable_bandit_force();
                        if (bandit_force_id != -1)
                        {
                            move_force_to_index(bandit_force_id, 0);
                            g_auto_skip_turn = true;
                            // 如果是正常势力且为玩家，记录当前玩家及其同盟信息，以便于设置下个贼势力的同盟(开视野)
                            _record_ally_forces(first_force);
                            pk::trace("联机扩展onNewDay,第一个force" + pk::decode(pk::get_name(first_force)) + "为玩家,插入贼势力");
                        }
                    }
                }
            }

            print_turn_table();

            pk::trace("联机扩展onNewDay...END");
        }

        void onTurnStart(pk::force@ force)
        {
            pk::trace("联机扩展onTurnStart... " + pk::decode(pk::get_name(force)) + ", " + (force.is_player() ? "***玩家***" : "电脑"));
            if (player_count < 2)
                return;

            bool is_normal_force = pk::is_normal_force(force);

            // 当前为贼势力
            if (!is_normal_force)
            {
                // 当前为贼势力，检查下一个force是否为玩家，是则将当前贼势力设为玩家，存档并过回合
                pk::int_bool next_force_player = is_next_force_player();

                pk::trace("检查下一个force是否为玩家: is_next_force_player: " + next_force_player.second);
                if (next_force_player.second)
                {
                    pk::scenario@ scenario = pk::get_scenario();
                    int next_force_id = scenario.get_turn_table_force_id(next_force_player.first);
                    pk::force@ next_force = pk::get_force(next_force_id);
                    // pk::message_box(pk::encode("联机扩展onTurnStart...当前势力为") + pk::get_name(force) + pk::encode(",下一势力") + pk::get_name(next_force) + pk::encode("为玩家"));

                    set_force_to_player(force, player_count);
                    pk::save_game(31);

                    if (调试模式)
                        pk::message_box(pk::encode("联机扩展onTurnStart...当前force为贼势力") + pk::get_name(force) + pk::encode(",且下一势力") + pk::get_name(next_force) + pk::encode("为玩家,自动保存"));

                    pk::trace("联机扩展onTurnStart... 当前force为贼势力" + pk::decode(pk::get_name(force)) + ",且下一势力" + pk::decode(pk::get_name(next_force)) + "为玩家,自动保存完毕");
                    // return;
                }
            }

            if (force.is_player())
            {
                // 控制联机信息框的显示
                if (g_dlg_pvp_info is null || g_dlg_pvp_info.main_dlg is null)
                {
                    // 重建g_dlg_pvp_info，以避免被场景切掉
                    @g_dlg_pvp_info = DlgPVPInfo();
                    g_dlg_pvp_info.create();
                }

                if (is_normal_force)
                {
                    g_dlg_pvp_info.set_visible(false);
                }
                else
                {
                    int next_force_id = get_next_force_id_in_same_turn();
                    if (pk::is_valid_force_id(next_force_id))
                    {
                        pk::force@ next_force = pk::get_force(next_force_id);
                        g_dlg_pvp_info.set_action_player(pk::get_name(next_force));
                    }
                    else
                    {
                        g_dlg_pvp_info.set_action_player("");
                    }
                    g_dlg_pvp_info.set_visible(true);
                }
            }
            pk::trace("联机扩展onTurnStart...END");
        }

        void onTurnEnd(pk::force@ force)
        {
            pk::trace("联机扩展onTurnEnd..." + pk::decode(pk::get_name(force)) + ", " + (force.is_player() ? "***玩家***" : "电脑"));
            if (player_count < 2)
                return;

            // if (_is_just_loaded)
            // {
            //     // 如果是刚读档后第一次过回合，则不需再次自动存档
            //     _is_just_loaded = false;
            //     // pk::next_turn();
            //     pk::trace("联机扩展onTurnEnd...END SKIPPING AUTO-SAVE");
            //     return;
            // }

            if (pk::is_normal_force(force))
            {
                pk::scenario@ scenario = pk::get_scenario();
                int current_idx = scenario.get_turn_table_index();

                if (force.is_player())
                {
                    if (!_if_need_insert_bandit)
                        _if_need_insert_bandit = true;

                    // 如果是正常势力且为玩家，则把前一个贼势力移回原位并设回为ai
                    int prev_idx = current_idx - 1;
                    if (prev_idx >= 0)
                    {
                        int prev_force_id = scenario.get_turn_table_force_id(prev_idx);
                        pk::force@ prev_force = pk::get_force(prev_force_id);
                        if (!pk::is_normal_force(prev_force))
                        {
                            // 设回为ai
                            set_force_to_player(prev_force, -1);

                            // 移除同盟信息
                            for (int i = 0; i < 非贼势力_末; ++i)
                            {
                                if (prev_force.is_ally(i))
                                {
                                    prev_force.set_ally(i, false);
                                }
                            }

                            // 将贼势力移回原位
                            int origin_idx = get_origin_index_of_bandit_force(prev_force_id);
                            move_force_to_index(prev_force_id, origin_idx);
                            pk::trace("00000 prev_force_id: " + prev_force_id + ", origin_idx: " + origin_idx);

                            // 因为把贼势力从前面移回原位，因此当前的turn_table_index需减1
                            current_idx--;
                            scenario.set_turn_table_index(current_idx);
                            pk::trace("00000 current_idx: " + current_idx);

                            if (调试模式)
                                pk::message_box(pk::encode("当前force为") + pk::get_name(force) + pk::encode(",前一个force") + pk::get_name(prev_force) + pk::encode(",设回为ai并移回原位"));

                            pk::trace("当前force为" + pk::decode(pk::get_name(force)) + ",前一个force" + pk::decode(pk::get_name(prev_force)) + ",设回为ai并移回原位");
                            print_turn_table();
                        }
                    }

                    // 如果是正常势力且为玩家，记录当前玩家及其同盟信息，以便于设置下个贼势力的同盟(开视野)
                    _record_ally_forces(force);
                }

                if (_if_need_insert_bandit)
                {
                    // 当前为正常势力，检查下一个force是否为玩家，是则插入贼势力，同时设置贼势力的同盟(开视野)
                    pk::int_bool next_force_player = is_next_force_player();

                    if (next_force_player.second)
                    {
                        int next_force_id = scenario.get_turn_table_force_id(next_force_player.first);
                        pk::force@ next_force = pk::get_force(next_force_id);
                        pk::force@ bandit_force;
                        if (调试模式)
                            pk::message_box(pk::encode("当前force为") + pk::get_name(force) + pk::encode(",下个force") + pk::get_name(next_force) + pk::encode("为玩家,插入贼势力"));

                        int bandit_force_id = find_suitable_bandit_force();
                        if (bandit_force_id >= 0)
                        {
                            move_force_to_index(bandit_force_id, next_force_player.first);
                            g_auto_skip_turn = true;

                            @bandit_force = pk::get_force(bandit_force_id);
                            // set_force_to_player(bandit_force, player_count);
                            pk::trace("联机扩展onTurnEnd... 贼势力" + pk::decode(pk::get_name(bandit_force)) + "插入完毕");
                            pk::trace("联机扩展onTurnEnd... 新的turn_table:");
                            print_turn_table();
                        }
                        else
                        {
                            pk::trace("联机扩展onTurnEnd... ERROR!!! 未找到贼势力!!!");
                        }

                        // 设置贼势力的同盟(开视野)
                        if (bandit_force !is null)
                        {
                            for (uint i = 0; i < this.arr_ally_force.length; ++i)
                            {
                                int ally_force_id = this.arr_ally_force[i];
                                bandit_force.set_ally(ally_force_id, true);
                            }
                        }
                    }
                }
            }
            else
            {
                // 当前为贼势力
                if (force.is_player())
                {
                    g_dlg_pvp_info.set_visible(false);
                    g_auto_skip_turn = false;
                }
            }

            pk::trace("联机扩展onTurnEnd...END");
        }

        void onPersonSetMsgBox(pk::person@ person)
        {
            pk::scenario@ scenario = pk::get_scenario();
            int turn_idx = scenario.get_turn_table_index();
            int force_id = scenario.get_turn_table_force_id(turn_idx);

            if (!pk::is_valid_force_id(force_id))
                return;

            pk::force@ force = pk::get_force(force_id);

            if (!pk::is_normal_force(force) && force.is_player())
            {
                // 当前回合为贼势力玩家回合，跳过message box
                int dlg_id = 73;
                pk::dialog@ dlg73 = pk::get_dialog(dlg_id);
                if (dlg73 !is null)
                {
                    int start_id = pk::get_dlg_childstart(dlg_id);
                    int child_count = pk::get_dlg_childcount(dlg_id);

                    for (int i = start_id; i < start_id + child_count; ++i)
                    {
                        pk::widget@ widget = dlg73.find_child(i);
                        if (i != 2872)
                        {
                            widget.set_visible(false);
                        }
                        else
                        {
                            _dlg73_timer = pk::game::get_time();
                            pk::detail::funcref func = pk::widget_on_update_t(_dlg73_on_widget_update);
                            widget.on_widget_update(func);
                        }
                    }

                    // pk::widget@ widget = dlg73.find_child(2872);
                    // if (widget !is null)
                    // {
                    //     _dlg73_timer = pk::game::get_time();
                    //     pk::detail::funcref func = pk::widget_on_update_t(_dlg73_on_widget_update);
                    //     widget.on_widget_update(func);
                    // }
                }
            }
        }

        // void onGameDraw()
        // {
        //     if (player_count < 2)
        //         return;

        //     if (pk::game::get_time() - last_refresh_time < REFRESH_INTERVAL)
        //         return;

        //     last_refresh_time = pk::game::get_time();

        //     pk::trace("联机扩展自动刷新");
        // }

        // 记录当前玩家及其同盟信息，以便于设置下个贼势力的同盟(开视野)
        private void _record_ally_forces(pk::force@ force)
        {
            this.arr_ally_force.resize(0);
            this.arr_ally_force.insertLast(force.id);
            for (int i = 0; i < 非贼势力_末; i++)
            {
                if (force.is_ally(i))
                    this.arr_ally_force.insertLast(i);
            }
        }

        // private void _on_destory_dlg_pvp_info(pk::widget@ widget)
        // {
        //     g_dlg_pvp_info.reset();
        //     @g_dlg_pvp_info = null;
        //     pk::trace("ZZZZZ dlg_pvp_info destoryed, g_dlg_pvp_info was set to null");
        // }

        private void _dlg73_on_widget_update(pk::widget@ widget, uint delta)
        {
            // 延时关闭dlg73
            if (pk::game::get_time() - _dlg73_timer < 30)
                return;

            pk::dialog@ dlg73 = pk::get_dialog(73);
            if (dlg73 !is null)
            {
                // pk::left_click(widget);
                dlg73.close(1);
                pk::trace("已延时30ms关闭贼势力玩家的msg box");
            }
        }

    } // class Main

    Main main;

    // 获取同回合中下一行动势力的force_id
    int get_next_force_id_in_same_turn()
    {
        pk::scenario@ scenario = pk::get_scenario();
        int next_idx = scenario.get_turn_table_index() + 1;
        int turn_table_size = scenario.get_turn_table_size();

        if (next_idx >= turn_table_size)
            return -1;

        return scenario.get_turn_table_force_id(next_idx);
    }

    int get_player_count()
    {
        int count = 0;
        for (int i = 0; i < 势力_末; i++)
        {
            auto force_ = pk::get_force(i);
            if (!pk::is_alive(force_))
                continue;
            if (force_.is_player())
                count++;
        }

        return count;
    }

    bool is_my_force_turn()
    {
        return true;
    }

    /**
     * @brief 在turn_table中，选出适合的贼势力，以用于move_force_to_index
     *   规则：优先用没部队的贼，二动也不影响。其次用之前没用过的贼，或者部队最少的贼
     * @return force_id   返回贼势力的 ID
     */
    int find_suitable_bandit_force()
    {
        // 🟡🟡TODO: 按设计的规则选出适合的贼势力

        return g_arr_bandit_force_id[pk::rand(g_arr_bandit_force_id.length)];
    }

    int get_origin_index_of_bandit_force(int bandit_force_id)
    {
        int offset = 势力_盗贼 - bandit_force_id; // 势力_盗贼是最后一个贼势力
        pk::scenario@ scenario = pk::get_scenario();
        int turn_table_size = scenario.get_turn_table_size();

        return turn_table_size - offset;
    }

    /**
     * @brief 检查下一个行动势力是否是玩家
     * @return ret.first   下一个行动势力的turn_table_idx
     * @return ret.second  是否是玩家
     */
    pk::int_bool is_next_force_player()
    {
        pk::int_bool ret;
        pk::scenario@ scenario = pk::get_scenario();
        int curr_index = scenario.get_turn_table_index();
        int next_index = curr_index + 1;

        ret.first = -1;
        ret.second = false;

        // 已到turn_table末尾，返回
        if (next_index == scenario.get_turn_table_size())
            // next_index = 0;
            return ret;

        ret.first = next_index;

        // 检查下个行动势力是否为player，如是，则把贼势力移为下个位置
        int next_force_id = scenario.get_turn_table_force_id(next_index);
        if (pk::is_valid_force_id(next_force_id))
        {
            pk::force@ next_force = pk::get_force(next_force_id);
            if (next_force.is_player())
            {
                ret.second = true;
                return ret;
            }
        }

        return ret;
    }

    /**
     * @brief 通用移动函数：将指定势力移动到目标索引位置
     * @note 逻辑定义：无论 force_to_move 在前还是在后，执行后 force_to_move 都会
     * 占据 target_idx 这个位置，而原位置的势力及其后的势力会向后顺延。
     */
    void move_force_to_index(int force_to_move, int target_idx)
    {
        pk::scenario@ scenario = pk::get_scenario();
        if (scenario is null)
            return;

        int size = scenario.get_turn_table_size();
        int current_idx = -1;

        // 1. 查找当前位置
        for (int i = 0; i < size; ++i)
        {
            if (scenario.get_turn_table_force_id(i) == force_to_move)
            {
                current_idx = i;
                break;
            }
        }

        // 没找到或者已经在目标位置，直接跳过
        if (current_idx == -1 || current_idx == target_idx)
            return;

        // 2. 索引修正（核心逻辑）
        // 如果我们是从“上方”往下移，因为上方留出的空位会导致目标索引向上缩进，
        // 为了确保能精准落在“傅彤”之前，我们需要将目标索引减 1。
        int final_target = target_idx;
        if (current_idx < target_idx)
        {
            final_target = target_idx - 1;
        }

        // 3. 执行平移（这一步逻辑保持不变，但使用修正后的 final_target）
        if (current_idx < final_target)
        {
            for (int i = current_idx; i < final_target; ++i)
                scenario.set_turn_table(i, scenario.get_turn_table_force_id(i + 1));
        }
        else
        {
            for (int i = current_idx; i > final_target; --i)
                scenario.set_turn_table(i, scenario.get_turn_table_force_id(i - 1));
        }

        // 4. 写入势力
        scenario.set_turn_table(final_target, force_to_move);
    } // move_force_to_index

    void print_turn_table()
    {
        pk::scenario@ scenario = pk::get_scenario();
        int turn_table_size = scenario.get_turn_table_size();
        string output = "\n";

        output += pk::format("Current Turn: {}-{}-{}\n", pk::get_year(), pk::get_month(), pk::get_day());

        for (int i = 0; i < turn_table_size; ++i)
        {
            int force_id = scenario.get_turn_table_force_id(i);
            if (!pk::is_valid_force_id(force_id))
                continue;

            pk::force@ force = pk::get_force(force_id);
            output += pk::format("座次{}: {}, {}, {}\n", i, pk::decode(pk::get_name(force)), force_id, (force.is_player() ? "***玩家***" : "电脑"));
        }

        pk::trace(output);
    }

    // 重设turn_table
    void reset_turn_table(int turn_table_type)
    {
        pk::scenario@ scenario = pk::get_scenario();
        int turn_table_size = scenario.get_turn_table_size();
        array<int> force_ids;

        if (turn_table_type == 1) // 按势力id从小到大排序
        {
            // 记录turn_table的全部势力
            for (int i = 0; i < turn_table_size; i++)
            {
                int force_id = scenario.get_turn_table_force_id(i);
                force_ids.insertLast(force_id);
            }

            // 按force_id从小到大排序
            force_ids.sort(function(a, b) { return a < b; });

            // 重设turn_table
            for (int i = 0; i < turn_table_size; i++)
            {
                int force_id = force_ids[i];
                scenario.set_turn_table(i, force_id);
            }
        }
    }

    // 将指定势力设置为player
    // player_id: 玩家id [0 ~ 7], -1为电脑
    bool set_force_to_player(pk::force@ force, int player_id = -1)
    {
        force.set_player_id(player_id);
        force.set_player(player_id);
        force.update();

        return true;
    }

    class PVPInfo
    {
        uint player_count = 0;
        string my_force_name = "";
        string current_force_name = "";

        PVPInfo() {}
    }

    class DlgPVPInfo
    {
        pk::dialog@ main_dlg = null;
        private pk::force@ _force = null;
        private pk::text@ _txt_actioning_player = null;
        private int _dlg_x, _dlg_y = 0, _dlg_w = 240, _dlg_h = 42;
        // pk::detail::funcref on_widget_destory = null;

        private int REFRESH_INTERVAL = 1000; // 毫秒
        private int _last_refresh_time = 0;

        DlgPVPInfo() {}

        void create()
        {
            // pk::size resolution = pk::get_resolution();
            // _dlg_x = int((resolution.width - _dlg_w) / 2);

            @main_dlg = pk::new_dialog(false);
            main_dlg.set_pos(240, 0);
            main_dlg.set_size(_dlg_w, _dlg_h);

            pk::sprite9@ bg = main_dlg.create_sprite9(393);
            bg.set_size(_dlg_w, _dlg_h);
            bg.set_color(0x60808080);

            pk::text@ txt = main_dlg.create_text();
            txt.set_pos(10, 0);
            txt.set_size(120, 32);
            txt.set_text_font(FONT_BIG);
            txt.set_text(pk::encode("行动中玩家:"));

            @_txt_actioning_player = main_dlg.create_text();
            _txt_actioning_player.set_pos(140, 0);
            _txt_actioning_player.set_size(100, 32);
            _txt_actioning_player.set_text_font(FONT_BIG);
            pk::detail::funcref func = pk::widget_on_destroy_t(_on_text_action_player_destory);
            _txt_actioning_player.on_widget_destroy(func);

            func = pk::widget_on_update_t(_on_widget_update_txt_action_player);
            _txt_actioning_player.on_widget_update(func);
        }

        void set_visible(bool visible)
        {
            main_dlg.set_visible(visible);
        }

        void set_action_player(string player_name)
        {
            _txt_actioning_player.set_text("\x1b[1x" + player_name);
        }

        void reset()
        {
            @main_dlg = null;
            @_force = null;
            @_txt_actioning_player = null;
            // on_dlg_destory = null;
        }

        private void _on_widget_update_txt_action_player(pk::widget@ widget, uint delta)
        {
            int curr_time = pk::game::get_time();
            if (curr_time - _last_refresh_time < REFRESH_INTERVAL)
                return;

            _last_refresh_time = curr_time;

            pk::scenario@ scenario = pk::get_scenario();
            int turn_idx = scenario.get_turn_table_index();
            int force_id = scenario.get_turn_table_force_id(turn_idx);

            if (!pk::is_valid_force_id(force_id))
            {
                return;
            }

            @_force = pk::get_force(force_id);

            if (g_auto_skip_turn && !pk::is_normal_force(_force) && _force.is_player())
            {
                // 异族玩家势力时，自动过回合
                pk::detail::funcref func0 = pk::async_t(_auto_next_turn);
                // pk::async(func0);

                g_auto_skip_turn = false;
            }
            // pk::trace("======== PvP Info Refreshed ========");
        }

        private void _on_dlg_destory(pk::widget@ widget)
        {
            reset();
            pk::trace("_on_dlg_destory: DlgPVPInfo Destoryed");
        }

        private void _on_text_action_player_destory(pk::widget@ widget)
        {
            reset();
            @g_dlg_pvp_info = null;
            pk::trace("_on_text_action_player_destory: text_action_player Destoryed");
        }

        private void _auto_next_turn()
        {
            pk::next_turn();
            pk::trace("当前势力<" + pk::decode(pk::get_name(_force)) + ">为异族玩家势力，自动过回合");
        }
    }

    // // 暂时不用这个遮挡dialog，先注释掉
    // class DlgSoftBlocker
    // {
    //     private pk::dialog@ _main_dlg = null;
    //     DlgSoftBlocker() {}

    //     void create()
    //     {
    //         pk::size resolution = pk::get_resolution();

    //         @_main_dlg = pk::new_dialog(false);
    //         _main_dlg.set_size(resolution);

    //         pk::sprite9@ bg = _main_dlg.create_sprite9(70);
    //         bg.set_size(resolution);
    //         bg.set_color(0xA0808080);
    //     }

    //     void set_visible(bool visible)
    //     {
    //         _main_dlg.set_visible(visible);
    //     }
    // }

} // namespace 联机扩展
