namespace BlindAttack.Core
{
    /// <summary>
    /// Hazır haritalar (30x20). İki oyuncu da maç boyunca AYNI haritayı kullanır.
    /// Lejant:
    ///  .  çimen      ,  toprak yol     #  taş harabe    h  çalı çit
    ///  T  ağaç       R  kaya           ~  su            =  köprü
    ///  H  ev         S  önerilen kahraman başlangıcı    E  çıkış kapısı
    /// İlk satır haritanın en üstüdür (izometrik görünümde en arkası).
    /// </summary>
    public static class MapLibrary
    {
        public static readonly string[] Names = { "Nehir Vadisi", "Kale Bahçesi", "Taş Ocağı" };

        static readonly string[][] Layouts =
        {
            new[]
            {
                "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT",
                "T..TT.......T.~~...T.........T",
                "T.........T...~~..######.....T",
                "T.T...........~~..#....#.H...T",
                "T.....,,,,,,..==,,#.R..#...R.T",
                "T.....,.......~~.,#..........T",
                "T..hhhh..h....~~.,#.##.......T",
                "T.....,..h.T.~~..,......####.T",
                "T.T...,R.h..~~..T,......#....T",
                "T.....,.....~~...,.HH...#..,,E",
                "T..S,,,,,,,,==..,,,HH,,,,,,,,T",
                "T.......,...~~...........,R..T",
                "T..T.R..,.hh~~...........T...T",
                "T.......,...~~....#####..,...T",
                "T.hhh...,..T.~~...#...#..,H..T",
                "T...h...,.....~~..#...#..,...T",
                "T...h...,,,,..==,,,,,T#,,....T",
                "T....T....R...~~.R......R.T..T",
                "T.T.T..T..T.T.~~..TT.T.....T.T",
                "TTTTTTTTTTTTTTTTTTTTTTTTTTTTTT",
            },
            new[]
            {
                "h#############################",
                "h..H................#........#",
                "h.T......T..........#...####.#",
                "h...........hhhhhh..#,,,,,,,,E",
                "h..hhhhhh...........,........#",
                "h.....h.............,hhhhhh..#",
                "h.....h...,,,,,,,,,,.........#",
                "h.....h...,........,.......H.#",
                "h.....h...,..~=~~..,..h......#",
                "h.hhhhh...,T~~=~~~.,..h..h...#",
                "h.........,.~~=~~~T,..hR.h...#",
                "h...T..R..,..~=~~..,..h..h.T.#",
                "h.........,........,..h..h...#",
                "h...hhhhhh,,,,,,,,,,.....h...#",
                "h.......h,......R............#",
                "h....R..h,.............hhhhh.#",
                "h.S,,,,,h,.hhhhhhhh..........#",
                "h.......h...........T......H.#",
                "h..............T.............#",
                "hhhhhhhhhhhhhhhhhhhhhhhhhhhhhh",
            },
            new[]
            {
                "RRRRRRR#RRRRRRRRRRRRRR#RRRRRRR",
                "R......#...R.R..R.....#RR..R.R",
                "R......#...T....R.~~..#R.....R",
                "RR..HR.#....R.....~~..#......R",
                "R......#....RR.#......#...T..R",
                "R......#.R.....#...R..#....R.R",
                "R.R....######.R#...R.....R...R",
                "R......#.......#.R....#....R.R",
                "RR.T...#..R....#......#####..R",
                "R......#...........RR.#....R.R",
                "R.S,,,,#.........R....#......R",
                "R.R...,#.......#......#......R",
                "R..R..,,,,,,,,,,#####.#.R....R",
                "R......,.~~~...,..R...#...R..R",
                "R..T...#.~~~...,,,,,,,,......R",
                "R......#..............,,,,,,,E",
                "R......#..........R.R.#......R",
                "R.RR...#....RT.#...T..#......R",
                "R......#.......#..R..R#......R",
                "RRRRRRRRRRRRRRR#RRRRRR#RRRRRRR",
            },
        };

        public static int Count => Layouts.Length;

        public static GridMap Create(int index)
        {
            index = ((index % Count) + Count) % Count;
            return new GridMap(Names[index], Layouts[index]);
        }
    }
}
