;;; Mr.T 2.0.0.6: conservative planning layer for ABC conversion.
;;; Loaded after MrT_ABC2Font.lsp. The original mapping/parser is retained.

(defun A2F2:AmbiguousP (s / i ch found)
  ;; Vietnamese Unicode and TCVN3 occupy overlapping code points. Never
  ;; automatically interpret such a string, even when its style says ABC.
  (setq i 1 found nil)
  (if (or (vl-string-search "\\U+" (strcase s))
          (vl-string-search "%<" s)) (setq found T))
  (while (and (<= i (strlen s)) (not found))
    (setq ch (substr s i 1))
    (if (or (> (ascii ch) 255)
            (vl-string-search ch "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúý"))
      (setq found T))
    (setq i (1+ i)))
  found)

(defun A2F2:WideP (s / i n found)
  ;; True when S holds a character above 255 (or a \U+ escape): never TCVN3.
  (setq i 1 n (strlen s) found (if (vl-string-search "\\U+" (strcase s)) T nil))
  (while (and (not found) (<= i n))
    (if (> (ascii (substr s i 1)) 255) (setq found T))
    (setq i (1+ i)))
  found)

;;; Latin-1 letters used by Vietnamese Unicode. Every other TCVN3 code point
;;; (ß « æ Ö § ...) cannot appear in Vietnamese Unicode text.
(setq *A2F2-VietLatin1* "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúý")
;;; Technical symbols that are also TCVN3 codes count half (Ø15, 2×3, µm, ½).
(setq *A2F2-TechSymbols* "×Øøµ½·")

(defun A2F2:Tcvn3Score (s / i n ch score)
  ;; Evidence that S is TCVN3 bytes: TCVN3-only code points score 2, technical
  ;; symbols 1, Vietnamese Unicode letters and ASCII 0.
  (setq i 1 n (strlen s) score 0)
  (while (and (<= i n) (< score 2))
    (setq ch (substr s i 1))
    (if (and (assoc ch *A2FLT-Map*) (not (vl-string-search ch *A2F2-VietLatin1*)))
      (setq score (+ score (if (vl-string-search ch *A2F2-TechSymbols*) 1 2))))
    (setq i (1+ i)))
  score)

(defun A2F2:PlainAsciiP (s / i n ok)
  (setq i 1 n (strlen s) ok T)
  (while (and ok (<= i n))
    (if (> (ascii (substr s i 1)) 127) (setq ok nil))
    (setq i (1+ i)))
  ok)

(defun A2F2:TrueTypeSigP (sig)
  ;; sig = "typeface|fontfile|tablefile|bigfont". A typeface name (SetFont) or a
  ;; .ttf/.otf file means TrueType; Mr.T styles may have only the typeface.
  (and sig (or (/= (substr sig 1 1) "|")
               (wcmatch (strcase sig) "*.TTF*,*.OTF*,*.TTC*"))))

(defun A2F2:SymbolFontP (name / u)
  (setq u (strcase (if name name "")))
  (wcmatch u "*SYMBOL*,*WINGDING*,*WEBDING*,*GDT*,*SYMAP*,*SYMATH*,*SYASTRO*,*SYMETEO*,*SYMUSIC*,*ESRI*,*MARLETT*"))

(defun A2F2:InlineSymbolP (s / p e found)
  ;; True when an MTEXT \f override names a symbol font.
  (setq p 0 found nil)
  (while (and (not found) (setq p (vl-string-search "\\F" (strcase s) p)))
    (setq e (vl-string-search ";" s p))
    (if (and e (A2F2:SymbolFontP (substr s (+ p 3) (- e p 2)))) (setq found T))
    (setq p (+ p 2)))
  found)

(defun A2F2:Raw (obj / nm)
  (setq nm (vla-get-ObjectName obj))
  (if (and (member nm '("AcDbAttribute" "AcDbAttributeDefinition"))
           (vlax-property-available-p obj 'MTextAttribute)
           (= (vla-get-MTextAttribute obj) :vlax-true))
    (vla-get-MTextAttributeContent obj)
    (vla-get-TextString obj)))

(defun A2F2:InlineTarget (s / i n out ch c2 end token fieldEnd)
  ;; Change only inline font controls, leaving visible characters, nested
  ;; fields, formatting controls and escaped backslashes intact.
  (setq i 1 n (strlen s) out "")
  (while (<= i n)
    (setq ch (substr s i 1))
    (cond
      ((= (substr s i 2) "%<")
        (setq fieldEnd (A2FLT:FieldEnd s i)
              out (strcat out (substr s i (- fieldEnd i))) i fieldEnd))
      ((and (= ch "\\") (< i n))
        (setq c2 (substr s (1+ i) 1))
        (if (and (member c2 '("f" "F")) (setq end (vl-string-search ";" s i)))
          (progn
            (setq token (substr s (+ i 2) (- end i 1)))
            (setq out (strcat out "\\f" (A2FLT:TargetFontToken token) ";") i (+ end 2)))
          (setq out (strcat out ch c2) i (+ i 2))))
      (T (setq out (strcat out ch) i (1+ i)))))
  out)

;;; Mr.T 2.0.1.8: one question only (target font). The last choice is the default,
;;; the Text Style is always the standard Mr.T_<FONT> style, and ABC/Unicode is
;;; detected per object instead of being asked.
(defun A2F2:LastTarget (/ v)
  (setq v (vl-catch-all-apply 'getenv (list "MrT_FontTarget")))
  (if (and v (not (vl-catch-all-error-p v)) (member v '("Bahnschrift" "Arial" "ArialNarrow" "TimesNewRoman"))) v "Bahnschrift"))

(defun A2FLT:ChooseTarget (/ kw last)
  (setq last (A2F2:LastTarget))
  (initget "Bahnschrift Arial ArialNarrow TimesNewRoman")
  (setq kw (getkword (strcat "\nFont đích [Bahnschrift/Arial/ArialNarrow/TimesNewRoman] <" last ">: ")))
  (if (not kw) (setq kw last))
  (vl-catch-all-apply 'setenv (list "MrT_FontTarget" kw))
  (cond ((equal kw "Arial") (A2FLT:SetTargetArial))
        ((equal kw "ArialNarrow") (A2FLT:WarnArialNarrow) (A2FLT:SetTargetArialNarrow))
        ((equal kw "TimesNewRoman") (A2FLT:SetTargetTimesNewRoman))
        (T (A2FLT:SetTargetBahnschrift))))

;;; Standard style: Mr.T_<FONT>, height 0, width 1, oblique 0. An existing style with
;;; that name pointing elsewhere is never edited; a _UNICODE variant is used instead.
(defun A2F2:AutoStyle (doc)
  (setq *A2FLT-StyleName* (A2FLT:ResolveTargetStyleName doc *A2FLT-StyleName*))
  (prompt (strcat "\nText Style chuẩn: " *A2FLT-StyleName*
    (if (A2FLT:StyleObject doc *A2FLT-StyleName*) " (đã có, dùng lại)." " (sẽ tạo khi áp dụng).")))
  (princ))

;;; Kept for compatibility with older scripts; no longer called by MRTFONT.
(defun A2F2:ChooseStyle (doc / name sty fi ok)
  (setq ok nil)
  (while (not ok)
    (setq name (getstring T "\nTên Text Style có sẵn (Enter = style Mr.T tự tạo): "))
    (if (= name "")
      (progn
        (setq *A2FLT-StyleName* (A2FLT:ResolveTargetStyleName doc *A2FLT-StyleName*))
        (setq ok T))
      (progn
        (setq sty (A2FLT:StyleObject doc name))
        (if sty
          (progn
            (setq fi (A2FLT:GetStyleFontInfo sty))
            (if (or (= (strcase (car fi)) (strcase *A2FLT-Typeface*))
                    (and (/= (cadr fi) "") (vl-filename-base (cadr fi))
                    (= (strcase (vl-filename-base (cadr fi)))
                       (strcase (vl-filename-base *A2FLT-FontFileName*)))))
              (progn
                (setq *A2FLT-StyleName* (vla-get-Name sty)
                      *A2FLT-TargetStyleReady* T *A2FLT-TargetStyleObject* sty ok T)
                (prompt "\nStyle có sẵn giữ thông số riêng; chiều cao, độ rộng và cách hiển thị có thể thay đổi."))
              (prompt "\nStyle này không dùng font đích đã chọn; nhập style khác.")))
          (prompt "\nKhông tìm thấy style; nhập lại hoặc Enter để tạo style Mr.T.")))))
  (princ))

(defun A2FLT:ProcessTextObject (doc obj forceMode / nm layer raw sn mt si ii abc h new)
  ;; This phase is read-only: no styles or objects are modified.
  (setq nm (vla-get-ObjectName obj))
  (cond
    ((not (A2FLT:TextObjectNameP nm)) 0)
    ((and (= nm "AcDbMLeader") (/= (vla-get-ContentType obj) 2)) 0)
    ((progn (setq layer (tblsearch "LAYER" (vla-get-Layer obj)))
            (and layer (/= 0 (logand 4 (cdr (assoc 70 layer))))))
      (setq *A2FLT-Locked* (1+ *A2FLT-Locked*)) 0)
    (T
      (setq raw (A2F2:Raw obj) sn (A2FLT:ObjectStyleName obj)
            mt (or (member nm '("AcDbMText" "AcDbMLeader"))
                   (and (member nm '("AcDbAttribute" "AcDbAttributeDefinition"))
                        (vlax-property-available-p obj 'MTextAttribute)
                        (= (vla-get-MTextAttribute obj) :vlax-true)))
            si (A2FLT:StyleInfoCached doc sn)
            ii (if mt (A2FLT:InlineABCInfo raw) '(nil nil))
            abc (or (car si) (car ii)))
      (cond
        ;; The style alone cannot disambiguate Latin-1 Vietnamese, e.g. PHÒNG.
        ((and abc (A2F2:AmbiguousP raw)) (A2F2:Hold obj raw))
        (abc
          (A2F2:AddPlan obj raw sn
            (A2F2:ApplyCase (A2FLT:ConvertString raw (car si) (cadr si) mt nil) mt)
            (A2F2:PromptPlan obj nm si)))
        ;; Not ABC but the characters still look like legacy encoding: never just restyle.
        ;; TCVN3 only uses codes <= 255, so a string with a real Unicode letter
        ;; (ổ, ư, đ ...) is Unicode even when it also has Ò/Ô (e.g. "PHÒNG MỔ").
        ;; Vietnamese Unicode letters inside Latin-1 (í, é, Ò ...) are not evidence.
        ((and (>= (A2F2:Tcvn3Score raw) 2) (not (A2F2:WideP raw))) (A2F2:Hold obj raw))
        ;; Kept for review: SHX text with non-ASCII characters (encoding unknown),
        ;; symbol fonts, and MTEXT that overrides into a symbol font.
        ((or (A2F2:SymbolFontP (caddr si))
             (and (not (A2F2:TrueTypeSigP (caddr si))) (not (A2F2:PlainAsciiP raw)))
             (and mt (A2F2:InlineSymbolP raw)))
          (A2F2:Hold obj raw))
        ;; Already Unicode: Text Style changes; MTEXT font overrides \f follow the target.
        (T
          (A2F2:AddPlan obj raw sn
            (A2F2:ApplyCase (if mt (A2F2:InlineTarget raw) raw) mt)
            (A2F2:PromptPlan obj nm si)))))))

(defun A2F2:Hold (obj raw)
  (setq *A2FLT-Ambiguous* (1+ *A2FLT-Ambiguous*))
  (if (< (length *A2F2-Examples*) 12)
    (setq *A2F2-Examples* (cons (strcat "? " (vla-get-Handle obj) " | " raw) *A2F2-Examples*)))
  0)

(defun A2F2:AddPlan (obj raw sn new pp)
  ;; pp = (oldPrompt . newPrompt) for an ATTDEF whose prompt is TCVN3, else nil.
  (setq *A2F2-Plans* (cons (list obj raw sn new pp) *A2F2-Plans*))
  (if (< (length *A2F2-Examples*) 12)
    (setq *A2F2-Examples*
      (cons (strcat "+ " (vla-get-Handle obj) " | " raw
                    (if (/= raw new) (strcat " -> " new) "")
                    (if pp (strcat " | prompt -> " (cdr pp)) ""))
            *A2F2-Examples*)))
  1)

(defun A2F2:PromptPlan (obj nm si / p n)
  ;; ATTDEF prompt (shown in the attribute dialog) is converted when it is TCVN3.
  (if (and (= nm "AcDbAttributeDefinition")
           (setq p (vl-catch-all-apply 'vla-get-PromptString (list obj)))
           (not (vl-catch-all-error-p p)) (/= p "")
           (not (A2F2:AmbiguousP p))
           (or (car si) (>= (A2F2:Tcvn3Score p) 2)))
    (progn (setq n (A2FLT:ConvertString p T nil nil nil)) (if (/= n p) (cons p n)))))

;;; ---------- Case option: GiuNguyen / Thuong / Hoa / Proper ----------
;;; Only visible characters change; MTEXT codes (\P \H..; \f..; {}), %% codes,
;;; fields %<..>% and \U+XXXX escapes stay exactly as they are.
(setq *A2F2-Case* "GiuNguyen")
(setq *A2F2-LowerMap* (mapcar '(lambda (p) (cons (cdr p) (car p))) *A2FLT-UpperMap*))

(defun A2F2:ChooseCase (/ kw)
  (initget "GiuNguyen Thuong Hoa Proper")
  (setq kw (getkword "\nKiểu chữ [GiuNguyen/Thuong/Hoa/Proper] <GiuNguyen>: "))
  (setq *A2F2-Case* (if kw kw "GiuNguyen")))

(defun A2F2:Up (ch / p)
  (cond ((setq p (assoc ch *A2FLT-UpperMap*)) (cdr p)) ((< (ascii ch) 128) (strcase ch)) (T ch)))
(defun A2F2:Low (ch / p)
  (cond ((setq p (assoc ch *A2F2-LowerMap*)) (cdr p)) ((< (ascii ch) 128) (strcase ch T)) (T ch)))
(defun A2F2:WordCharP (ch / a)
  ;; Letters, digits and combining marks (U+0300-U+036F) continue a word.
  (setq a (ascii ch))
  (or (/= (A2F2:Up ch) (A2F2:Low ch)) (and (>= a 48) (<= a 57)) (and (>= a 768) (<= a 879))))

(defun A2F2:WordEnd (s i / n)
  ;; One-based position after the word starting at I.
  (setq n (strlen s))
  (while (and (<= i n) (A2F2:WordCharP (substr s i 1))) (setq i (1+ i)))
  i)

(defun A2F2:ApplyCase (s isMText / i n out ch c2 end start)
  (if (or (= *A2F2-Case* "GiuNguyen") (not s) (= s ""))
    s
    (progn
      (setq i 1 n (strlen s) out "" start T)
      (while (<= i n)
        (setq ch (substr s i 1))
        (cond
          ((= (substr s i 2) "%<")
            (setq end (A2FLT:FieldEnd s i) out (strcat out (substr s i (- end i))) i end start nil))
          ((= (substr s i 2) "%%")
            (setq end (+ i 3))
            (if (and (<= (+ i 4) n) (wcmatch (substr s (+ i 2) 3) "###")) (setq end (+ i 5)))
            (setq out (strcat out (substr s i (- end i))) i end start T))
          ((and isMText (= ch "\\") (< i n))
            (setq c2 (substr s (1+ i) 1))
            (cond
              ((and (= (strcase c2) "U") (= (substr s (+ i 2) 1) "+"))
                (setq out (strcat out (substr s i 7)) i (+ i 7) start nil))
              ((and (member c2 '("S" "s")) (setq end (vl-string-search ";" s i)))
                ;; Stacked text (\S1^2;) is visible content.
                (setq out (strcat out ch c2 (A2F2:ApplyCase (substr s (+ i 2) (- end i 1)) nil) ";")
                      i (+ end 2)))
              ((and (A2FLT:MTextArgCodeP c2) (setq end (vl-string-search ";" s i)))
                (setq out (strcat out (substr s i (- (+ end 2) i))) i (+ end 2)))
              (T
                (if (member c2 '("P" "~")) (setq start T))
                (setq out (strcat out ch c2) i (+ i 2)))))
          ((and isMText (member ch '("{" "}"))) (setq out (strcat out ch) i (1+ i)))
          ;; Proper/Thuong keep technical codes (a word with a digit: MA4, CO2, D110, KYT04).
          ((and start (/= *A2F2-Case* "Hoa") (A2F2:WordCharP ch)
                (setq end (A2F2:WordEnd s i)) (wcmatch (substr s i (- end i)) "*#*"))
            (setq out (strcat out (substr s i (- end i))) i end start nil))
          (T
            (setq out (strcat out
              (cond ((= *A2F2-Case* "Hoa") (A2F2:Up ch))
                    ((= *A2F2-Case* "Thuong") (A2F2:Low ch))
                    (start (A2F2:Up ch))
                    (T (A2F2:Low ch)))))
            (setq start (not (A2F2:WordCharP ch)) i (1+ i)))))
      out)))

;;; ---------- Block definitions reached from selected INSERTs ----------
(setq *A2F2-Visited* nil *A2F2-BlockDefs* nil)
(defun A2F2:ScanBlockDef (doc name / blk)
  ;; Each definition once; xrefs and layouts are never edited. Nested blocks follow.
  (if (and name (/= name "") (not (member (strcase name) *A2F2-Visited*)))
    (progn
      (setq *A2F2-Visited* (cons (strcase name) *A2F2-Visited*))
      (setq blk (vl-catch-all-apply 'vla-Item (list (vla-get-Blocks doc) name)))
      (if (and (not (vl-catch-all-error-p blk))
               (/= (vla-get-IsXRef blk) :vlax-true)
               (/= (vla-get-IsLayout blk) :vlax-true))
        (vlax-for o blk
          (A2FLT:CountResult (A2FLT:SafeProcess doc o nil))
          (if (= (vla-get-ObjectName o) "AcDbBlockReference")
            (progn
              (vl-catch-all-apply 'A2FLT:SelectedAttributes (list doc o nil))
              (A2F2:ScanInsert doc o))))))))
(defun A2F2:ScanInsert (doc ref / en)
  (A2F2:ScanBlockDef doc (vla-get-Name ref))
  ;; Dynamic blocks: the visible *U copy and the master definition.
  (if (vlax-property-available-p ref 'EffectiveName)
    (setq en (vl-catch-all-apply 'vla-get-EffectiveName (list ref))))
  (if (and en (not (vl-catch-all-error-p en))) (A2F2:ScanBlockDef doc en)))
(defun A2F2:HasInsertP (ss / i found)
  (setq i 0 found nil)
  (while (and (not found) (< i (sslength ss)))
    (if (= (cdr (assoc 0 (entget (ssname ss i)))) "INSERT") (setq found T))
    (setq i (1+ i)))
  found)

(defun A2FLT:Execute (selected / *error* doc undoOpen ss i obj plan r confirmed oldEcho count errors choice)
  (defun *error* (msg)
    (if undoOpen (vl-catch-all-apply 'vla-EndUndoMark (list doc)))
    (if oldEcho (setvar "CMDECHO" oldEcho))
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*,*EXIT*")))
      (prompt (strcat "\nMr.T font: " msg)))
    (if undoOpen (prompt "\nDùng UNDO để hoàn tác lượt chuyển.")) (princ))
  (if (A2FLT:Preflight)
    (progn
      (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
      ;; No mode question: each object is detected (ABC → chuyển mã; Unicode → chỉ đổi style).
      (setq *A2F2-Mode* "Auto" *A2F2-Selected* selected)
      (prompt "\nTự nhận diện: chữ ABC/TCVN3 được chuyển mã; chữ Unicode đổi sang style chuẩn; MTEXT đổi cả font riêng \\f.")
      (if (= (strcase *A2FLT-Typeface*) "BAHNSCHRIFT")
        (prompt "\nLưu ý: Bahnschrift trên máy thử thiếu glyph ỷ; kiểm tra font trên máy và bản vẽ thực tế."))
      (A2F2:ChooseCase)
      (setq *A2F2-BlockDefs* nil *A2F2-Visited* nil)
      (if selected
        (progn
          (prompt "\nChọn TEXT, MTEXT, thuộc tính, MLEADER hoặc block: ")
          ;; (or a b) returns T, not the selection set: use cond to keep the value.
          (setq ss (cond (*MRTFONT-Preselection*)
                         ((ssget '((0 . "TEXT,MTEXT,ATTDEF,ATTRIB,MULTILEADER,INSERT"))))))
          (setq *MRTFONT-Preselection* nil)
          (if (and ss (A2F2:HasInsertP ss))
            (progn
              (initget "Co Khong")
              (setq *A2F2-BlockDefs*
                (/= (getkword "\nSửa cả chữ/ATTDEF bên trong block đã chọn (mọi bản chèn cùng tên đổi theo) [Co/Khong] <Co>: ") "Khong"))))))
      (if (or (not selected) ss)
        (progn
          (A2FLT:ResetRunCaches doc)
          (A2F2:AutoStyle doc)
          (setq *A2FLT-Converted* 0 *A2FLT-Errors* 0 *A2FLT-Skipped* 0
                *A2FLT-Locked* 0 *A2FLT-Ambiguous* 0 *A2F2-Plans* nil *A2F2-Examples* nil)
          (if selected
            (progn (setq i 0)
              (repeat (sslength ss)
                (setq obj (vlax-ename->vla-object (ssname ss i)))
                (A2FLT:CountResult (A2FLT:SafeProcess doc obj nil))
                (vl-catch-all-apply 'A2FLT:SelectedAttributes (list doc obj nil))
                (if (and *A2F2-BlockDefs* (= (vla-get-ObjectName obj) "AcDbBlockReference"))
                  (vl-catch-all-apply 'A2F2:ScanInsert (list doc obj)))
                (setq i (1+ i))))
            (A2FLT:ScanAll doc))
          (prompt (strcat "\nMr.T font — xem trước; kiểu chữ: " *A2F2-Case* "; font đích: " *A2FLT-Typeface*
            " | Text Style: " *A2FLT-StyleName*
            "\nCó thể chuyển: " (itoa (length *A2F2-Plans*))
            " | Cần kiểm tra bảng mã: " (itoa *A2FLT-Ambiguous*)
            " | Layer khóa: " (itoa *A2FLT-Locked*) " | Lỗi quét: " (itoa *A2FLT-Errors*)
            "\nCác mẫu tối đa 12 dòng (+ chuyển, ? cần kiểm tra); F2 xem lịch sử:"))
          (foreach r (reverse *A2F2-Examples*) (prompt (strcat "\n" r)))
          (if *A2F2-Plans*
            (progn
              (initget "Yes No")
              (setq confirmed (getkword "\nÁp dụng? Block dùng chung ảnh hưởng mọi tham chiếu; UNDO để hoàn tác. [Yes/No] <Yes>: "))
              (if (not (equal confirmed "No"))
                (progn
                  (setq oldEcho (getvar "CMDECHO") count 0 errors 0)
                  (setvar "CMDECHO" 0) (vla-StartUndoMark doc) (setq undoOpen T)
                  (A2FLT:EnsureStyle doc)
                  (foreach plan (reverse *A2F2-Plans*)
                    (setq obj (car plan))
                    (setq r (vl-catch-all-apply 'A2F2:ApplyOne (list obj (cadr plan) (caddr plan) (cadddr plan) (nth 4 plan))))
                    (if (or (vl-catch-all-error-p r) (not r))
                      (setq errors (1+ errors)) (setq count (1+ count))))
                  (vla-EndUndoMark doc) (setq undoOpen nil) (setvar "CMDECHO" oldEcho)
                  (vla-Regen doc 1)
                  (prompt (strcat "\nĐã chuyển: " (itoa count) " | Lỗi/đã thay đổi: " (itoa errors)
                    " | Cần kiểm tra: " (itoa *A2FLT-Ambiguous*) ". Dùng UNDO để hoàn tác.")))
                (prompt "\nĐã hủy, không thay đổi bản vẽ.")))
            (prompt "\nKhông có mục đủ điều kiện tự chuyển.")))
        (prompt "\nKhông có đối tượng được chọn.")))) (princ))

(defun A2F2:ApplyOne (obj raw style new pp / r)
  (setq r (A2F2:ApplyText obj raw style new))
  ;; ATTDEF prompt: only after the text/style write succeeded, and only if unchanged since preview.
  (if (and r pp (= (vla-get-PromptString obj) (car pp)))
    (if (vl-catch-all-error-p (vl-catch-all-apply 'vla-put-PromptString (list obj (cdr pp))))
      (prompt (strcat "\nKhông đổi được prompt ATTDEF " (vla-get-Handle obj)))))
  r)

(defun A2F2:ApplyText (obj raw style new / r restore)
  (if (and (= raw (A2F2:Raw obj)) (= (strcase style) (strcase (A2FLT:ObjectStyleName obj))))
    (progn
      (setq r (if (= raw new)
                (vl-catch-all-apply 'A2F2:StyleOnly (list obj *A2FLT-StyleName*))
                (vl-catch-all-apply 'A2FLT:CommitText (list obj new *A2FLT-StyleName*))))
      (if (or (vl-catch-all-error-p r) (not r))
        (progn
          (setq restore (if (= raw new)
                          (vl-catch-all-apply 'A2F2:StyleOnly (list obj style))
                          (vl-catch-all-apply 'A2FLT:CommitText (list obj raw style))))
          (if (or (vl-catch-all-error-p restore) (not restore))
            (prompt "\nKhông phục hồi được đối tượng lỗi; hãy dùng UNDO.")) nil)
        T)) nil))

(defun A2F2:StyleOnly (obj style)
  (A2FLT:SetObjectStyle obj style)
  (= (strcase style) (strcase (A2FLT:ObjectStyleName obj))))

(princ)
