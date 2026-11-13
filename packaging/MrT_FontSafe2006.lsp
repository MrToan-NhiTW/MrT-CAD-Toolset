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

(defun A2FLT:ChooseTarget (/ kw)
  (initget "Bahnschrift Arial TimesNewRoman")
  (setq kw (getkword "\nFont đích [Bahnschrift/Arial/TimesNewRoman] <Bahnschrift>: "))
  (cond ((equal kw "Arial") (A2FLT:SetTargetArial))
        ((equal kw "TimesNewRoman") (A2FLT:SetTargetTimesNewRoman))
        (T (A2FLT:SetTargetBahnschrift))))

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
        ((equal *A2F2-Mode* "Unicode")
          ;; Whole drawing: require identifiable TrueType; skip inline fonts.
          ;; Explicitly selected objects: normalize font overrides, not text.
          (if (or (and (not *A2F2-Selected*)
                       (or abc (not (vl-string-search ".TTF" (strcase (caddr si))))))
                  (and (not *A2F2-Selected*) mt
                       (or (vl-string-search "\\F" (strcase raw))
                           (vl-string-search "%<" raw))))
            (progn
              (setq *A2FLT-Ambiguous* (1+ *A2FLT-Ambiguous*))
              (if (< (length *A2F2-Examples*) 12)
                (setq *A2F2-Examples* (cons (strcat "? " (vla-get-Handle obj) " | " raw) *A2F2-Examples*))) 0)
            (progn
              (setq new (if (and *A2F2-Selected* mt) (A2F2:InlineTarget raw) raw))
              (setq *A2F2-Plans* (cons (list obj raw sn new) *A2F2-Plans*))
              (if (< (length *A2F2-Examples*) 12)
                (setq *A2F2-Examples* (cons (strcat "+ " (vla-get-Handle obj) " | " raw) *A2F2-Examples*))) 1)))
        ((not abc)
          (if (A2FLT:LegacyContentPFast raw)
            (setq *A2FLT-Ambiguous* (1+ *A2FLT-Ambiguous*))) 0)
        ((A2F2:AmbiguousP raw)
          (setq *A2FLT-Ambiguous* (1+ *A2FLT-Ambiguous*))
          (if (< (length *A2F2-Examples*) 12)
            (setq *A2F2-Examples* (cons (strcat "? " (vla-get-Handle obj) " | " raw) *A2F2-Examples*))) 0)
        (T
          (setq h (cadr si)
                new (A2FLT:ConvertString raw (car si) h mt nil))
          (setq *A2F2-Plans* (cons (list obj raw sn new) *A2F2-Plans*))
          (if (< (length *A2F2-Examples*) 12)
            (setq *A2F2-Examples* (cons (strcat "+ " (vla-get-Handle obj) " | " raw " -> " new) *A2F2-Examples*)))
          1)))))

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
      (initget "ABC FontChiDinh")
      (setq choice (getkword "\nChế độ [ABC/FontChiDinh] <ABC>: "))
      (setq *A2F2-Mode* (if (equal choice "FontChiDinh") "Unicode" "ABC")
            *A2F2-Selected* selected)
      (if (equal *A2F2-Mode* "Unicode")
        (prompt "\nFont chỉ định giữ chữ, đổi style; với lựa chọn thủ công sẽ đổi cả font riêng trong MTEXT. Chữ mã ABC có thể hiện sai nếu chỉ đổi font."))
      (if (= (strcase *A2FLT-Typeface*) "BAHNSCHRIFT")
        (prompt "\nLưu ý: Bahnschrift trên máy thử thiếu glyph ỷ; kiểm tra font trên máy và bản vẽ thực tế."))
      (if selected
        (progn
          (prompt "\nChọn chữ hoặc block: thuộc tính được chọn; định nghĩa block dùng chung chỉ quét ở chế độ toàn bản vẽ. ")
          (setq ss (or *MRTFONT-Preselection* (ssget '((0 . "TEXT,MTEXT,ATTDEF,ATTRIB,MULTILEADER,INSERT")))))
          (setq *MRTFONT-Preselection* nil)))
      (if (or (not selected) ss)
        (progn
          (A2FLT:ResetRunCaches doc)
          (A2F2:ChooseStyle doc)
          (setq *A2FLT-Converted* 0 *A2FLT-Errors* 0 *A2FLT-Skipped* 0
                *A2FLT-Locked* 0 *A2FLT-Ambiguous* 0 *A2F2-Plans* nil *A2F2-Examples* nil)
          (if selected
            (progn (setq i 0)
              (repeat (sslength ss)
                (setq obj (vlax-ename->vla-object (ssname ss i)))
                (A2FLT:CountResult (A2FLT:SafeProcess doc obj nil))
                (A2FLT:SelectedAttributes doc obj nil)
                (setq i (1+ i))))
            (A2FLT:ScanAll doc))
          (prompt (strcat "\nMr.T font — xem trước; chế độ: " *A2F2-Mode* "; font đích: " *A2FLT-Typeface*
            " | Text Style: " *A2FLT-StyleName*
            "\nCó thể chuyển: " (itoa (length *A2F2-Plans*))
            " | Cần kiểm tra bảng mã: " (itoa *A2FLT-Ambiguous*)
            " | Layer khóa: " (itoa *A2FLT-Locked*) " | Lỗi quét: " (itoa *A2FLT-Errors*)
            "\nCác mẫu tối đa 12 dòng (+ chuyển, ? cần kiểm tra); F2 xem lịch sử:"))
          (foreach r (reverse *A2F2-Examples*) (prompt (strcat "\n" r)))
          (if *A2F2-Plans*
            (progn
              (initget "Yes No")
              (setq confirmed (getkword "\nThay các mục có thể chuyển? Block dùng chung ảnh hưởng mọi tham chiếu. [Yes/No] <No>: "))
              (if (equal confirmed "Yes")
                (progn
                  (setq oldEcho (getvar "CMDECHO") count 0 errors 0)
                  (setvar "CMDECHO" 0) (vla-StartUndoMark doc) (setq undoOpen T)
                  (A2FLT:EnsureStyle doc)
                  (foreach plan (reverse *A2F2-Plans*)
                    (setq obj (car plan))
                    (setq r (vl-catch-all-apply 'A2F2:ApplyOne (list obj (cadr plan) (caddr plan) (cadddr plan))))
                    (if (or (vl-catch-all-error-p r) (not r))
                      (setq errors (1+ errors)) (setq count (1+ count))))
                  (vla-EndUndoMark doc) (setq undoOpen nil) (setvar "CMDECHO" oldEcho)
                  (vla-Regen doc 1)
                  (prompt (strcat "\nĐã chuyển: " (itoa count) " | Lỗi/đã thay đổi: " (itoa errors)
                    " | Cần kiểm tra: " (itoa *A2FLT-Ambiguous*) ". Dùng UNDO để hoàn tác.")))
                (prompt "\nĐã hủy, không thay đổi bản vẽ.")))
            (prompt "\nKhông có mục đủ điều kiện tự chuyển.")))
        (prompt "\nKhông có đối tượng được chọn.")))) (princ))

(defun A2F2:ApplyOne (obj raw style new / r restore)
  (if (and (= raw (A2F2:Raw obj)) (= (strcase style) (strcase (A2FLT:ObjectStyleName obj))))
    (progn
      (setq r (if (and (equal *A2F2-Mode* "Unicode") (= raw new))
                (vl-catch-all-apply 'A2F2:StyleOnly (list obj *A2FLT-StyleName*))
                (vl-catch-all-apply 'A2FLT:CommitText (list obj new *A2FLT-StyleName*))))
      (if (or (vl-catch-all-error-p r) (not r))
        (progn
          (setq restore (if (and (equal *A2F2-Mode* "Unicode") (= raw new))
                          (vl-catch-all-apply 'A2F2:StyleOnly (list obj style))
                          (vl-catch-all-apply 'A2FLT:CommitText (list obj raw style))))
          (if (or (vl-catch-all-error-p restore) (not restore))
            (prompt "\nKhông phục hồi được đối tượng lỗi; hãy dùng UNDO.")) nil)
        T)) nil))

(defun A2F2:StyleOnly (obj style)
  (A2FLT:SetObjectStyle obj style)
  (= (strcase style) (strcase (A2FLT:ObjectStyleName obj))))

(princ)
