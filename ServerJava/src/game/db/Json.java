package game.db;

import java.util.*;

/**
 * JSON tối giản cho hồ sơ người chơi và các hàng tpl_*, repo không có thư viện JSON. Object là LinkedHashMap (giữ thứ tự
 * khóa để JsonUtility của client đọc lại y nguyên), mảng là ArrayList, số nguyên là Long, số lẻ là Double.
 */
public class Json {
    final String s;     // chuỗi đang đọc
    int i;              // vị trí đang đọc

    Json(String s) {
        this.s = s;
    }

    // ---------------- đọc ----------------

    /** Đọc cả chuỗi thành một giá trị. Còn ký tự thừa sau giá trị thì báo lỗi. */
    static Object parse(String text) {
        Json j = new Json(text);
        Object v = j.value();
        j.space();
        if (j.i != text.length()) throw new IllegalArgumentException("JSON thua ky tu o " + j.i);
        return v;
    }

    /** Đọc chuỗi JSON mà gốc là object. */
    @SuppressWarnings("unchecked")
    public static Map<String, Object> object(String text) {
        return (Map<String, Object>) parse(text);
    }

    /** Bỏ qua khoảng trắng. */
    void space() {
        while (i < s.length() && Character.isWhitespace(s.charAt(i))) i++;
    }

    /** Đọc một giá trị bất kỳ ở vị trí hiện tại. */
    Object value() {
        space();
        char c = s.charAt(i);
        if (c == '{') return map();
        if (c == '[') return list();
        if (c == '"') return string();
        if (word("true")) return Boolean.TRUE;
        if (word("false")) return Boolean.FALSE;
        if (word("null")) return null;
        return number();
    }

    /** Có từ w ở vị trí hiện tại thì đọc qua và trả true. */
    boolean word(String w) {
        if (!s.startsWith(w, i)) return false;
        i += w.length();
        return true;
    }

    /** Báo lỗi nếu ký tự kế tiếp không phải c. */
    void expect(char c) {
        if (s.charAt(i++) != c) throw new IllegalArgumentException("JSON thieu " + c + " o " + i);
    }

    /** Đọc object {...}, giữ thứ tự khóa. */
    Map<String, Object> map() {
        Map<String, Object> m = new LinkedHashMap<>();
        i++;
        space();
        if (s.charAt(i) == '}') {
            i++;
            return m;
        }
        while (true) {
            space();
            String key = string();
            space();
            expect(':');
            m.put(key, value());
            space();
            char c = s.charAt(i++);
            if (c == '}') return m;
            if (c != ',') throw new IllegalArgumentException("JSON sai o " + i);
        }
    }

    /** Đọc mảng [...]. */
    List<Object> list() {
        List<Object> l = new ArrayList<>();
        i++;
        space();
        if (s.charAt(i) == ']') {
            i++;
            return l;
        }
        while (true) {
            l.add(value());
            space();
            char c = s.charAt(i++);
            if (c == ']') return l;
            if (c != ',') throw new IllegalArgumentException("JSON sai o " + i);
        }
    }

    /** Đọc chuỗi "...", xử lý ký tự thoát. */
    String string() {
        StringBuilder b = new StringBuilder();
        i++;
        while (true) {
            char c = s.charAt(i++);
            if (c == '"') return b.toString();
            if (c != '\\') {
                b.append(c);
                continue;
            }
            char e = s.charAt(i++);
            switch (e) {
                case 'n': b.append('\n'); break;
                case 'r': b.append('\r'); break;
                case 't': b.append('\t'); break;
                case 'b': b.append('\b'); break;
                case 'f': b.append('\f'); break;
                case 'u':
                    b.append((char) Integer.parseInt(s.substring(i, i + 4), 16));
                    i += 4;
                    break;
                default: b.append(e);
            }
        }
    }

    /** Đọc số: có dấu chấm hoặc mũ thì là Double, không thì Long. */
    Number number() {
        int start = i;
        boolean real = false;
        while (i < s.length() && "+-0123456789.eE".indexOf(s.charAt(i)) >= 0) {
            real |= ".eE".indexOf(s.charAt(i)) >= 0;
            i++;
        }
        String n = s.substring(start, i);
        if (n.isEmpty()) throw new IllegalArgumentException("JSON sai o " + i);
        return real ? (Number) Double.valueOf(n) : (Number) Long.valueOf(n);
    }

    // ---------------- ghi ----------------

    /** Ghi giá trị (Map, List, String, số, Boolean, null) thành chuỗi JSON không khoảng trắng. */
    public static String write(Object v) {
        StringBuilder b = new StringBuilder();
        write(b, v);
        return b.toString();
    }

    /** Ghi giá trị vào b. Kiểu khác Map / List / String / null thì ghi bằng toString. */
    @SuppressWarnings("unchecked")
    static void write(StringBuilder b, Object v) {
        if (v == null) {
            b.append("null");
        } else if (v instanceof String) {
            quote(b, (String) v);
        } else if (v instanceof Map) {
            b.append('{');
            boolean first = true;
            for (Map.Entry<String, Object> e : ((Map<String, Object>) v).entrySet()) {
                if (!first) b.append(',');
                first = false;
                quote(b, e.getKey());
                b.append(':');
                write(b, e.getValue());
            }
            b.append('}');
        } else if (v instanceof List) {
            b.append('[');
            boolean first = true;
            for (Object o : (List<Object>) v) {
                if (!first) b.append(',');
                first = false;
                write(b, o);
            }
            b.append(']');
        } else {
            b.append(v);
        }
    }

    /** Ghi chuỗi có ngoặc kép, thoát ký tự đặc biệt. */
    public static void quote(StringBuilder sb, String s) {
        sb.append('"');
        for (int k = 0; k < s.length(); k++) {
            char c = s.charAt(k);
            if (c == '"' || c == '\\') sb.append('\\').append(c);
            else if (c == '\n') sb.append("\\n");
            else if (c == '\r') sb.append("\\r");
            else if (c == '\t') sb.append("\\t");
            else if (c < ' ') sb.append(String.format("\\u%04x", (int) c));
            else sb.append(c);
        }
        sb.append('"');
    }

    // ---------------- đọc trường có thể thiếu ----------------
    // hàng tpl_* bỏ cột NULL, hồ sơ cũ có thể chưa có trường mới: thiếu thì trả giá trị mặc định

    /** Trường số nguyên, thiếu thì 0. */
    public static long num(Map<String, Object> m, String key) {
        Object v = m.get(key);
        return v instanceof Number ? ((Number) v).longValue() : 0;
    }

    /** Trường số thực, thiếu thì 0. */
    public static double real(Map<String, Object> m, String key) {
        Object v = m.get(key);
        return v instanceof Number ? ((Number) v).doubleValue() : 0;
    }

    /** Trường true / false, thiếu thì false. */
    public static boolean bool(Map<String, Object> m, String key) {
        return Boolean.TRUE.equals(m.get(key));
    }

    /** Trường chuỗi, thiếu thì chuỗi rỗng. */
    public static String str(Map<String, Object> m, String key) {
        Object v = m.get(key);
        return v instanceof String ? (String) v : "";
    }

    /** Trường mảng object (chính danh sách trong m), thiếu thì một danh sách rỗng mới không gắn vào m. */
    @SuppressWarnings("unchecked")
    public static List<Map<String, Object>> rows(Map<String, Object> m, String key) {
        Object v = m.get(key);
        return v instanceof List ? (List<Map<String, Object>>) v : new ArrayList<>();
    }

    /** Trường mảng số nguyên, thiếu thì mảng rỗng. */
    public static int[] ints(Map<String, Object> m, String key) {
        Object v = m.get(key);
        if (!(v instanceof List)) return new int[0];
        List<?> l = (List<?>) v;
        int[] a = new int[l.size()];
        for (int k = 0; k < a.length; k++) a[k] = ((Number) l.get(k)).intValue();
        return a;
    }
}
