/*{
author=氕氘氚
version=1.0
date=2026/3/20
}*/
/***
联机扩展功能（过回合自动存档等）
配合三国志11联机工具使用
*/

namespace 联机扩展
{
    const int priority = 101;

    class Main
    {
        int player_count = 0;

        Main()
        {
            pk::bind(106, pk::trigger106_t(onLoad));
            pk::bind(111, priority, pk::trigger111_t(onTurnStart));
        }

        void onLoad(int file_id, pk::s11reader @r)
        {
            player_count = 0;
            for (int i = 0; i < 势力_末; i++)
            {
                auto force_ = pk::get_force(i);
                if (!pk::is_alive(force_)) continue;
                if (force_.is_player()) player_count++;
            }
        }

        void onTurnStart(pk::force @force)
        {
            if (player_count < 2) return;

            if (force.is_player())
            {
                pk::trace("联机扩展自动存档");
                pk::save_game(31);
            }
        }

    } // class Main

    Main main;

} // namespace 联机扩展
