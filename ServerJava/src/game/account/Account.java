package game.account;

/** Một hàng accounts ghép players, đọc lúc đăng nhập. */
public class Account {
    public int id, rounds, map, x, z;
    public String name, profile;
    public byte[] salt, hash;
}
