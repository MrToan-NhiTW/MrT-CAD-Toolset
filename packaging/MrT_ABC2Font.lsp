;;; ABC2FONT_LT2024_MrT_NhiTW.lsp — V1.9.12 LT2024 rebuild
;;; Based on ABC2FONT_V1.8_MrToan-NhiTW.lsp supplied by the user.
;;; Target: AutoCAD LT 2024+ / AutoCAD 2024+ for Windows; Unicode engine.
;;; Uses only AutoCAD internal ActiveX: no third-party COM, Express Tools or VBA.
;;; Auto detects actual font metadata; content fingerprints are diagnostic only.
;;; Commands retained: ABC2FONT, ABC2BAHN, ABC2ARIAL, ABC2TIMES, ABC2ARIALN
;;; and their _SEL variants. ABC2CHECK / ABC2HELP added or refreshed.
;;; All: model/layout/local block definitions; shared definitions affect all inserts.
;;; _SEL: selected text and variable attributes only; no block-definition changes.
;;; Excludes Xrefs, locked layers, tables, dimensions, non-text MLeaders.
;;; Save a DWG copy and validate layout after font replacement.
;;; Validation: see accompanying verification note; LT runtime not yet verified.
;;; workflow by Mr.T NhiTW

;; ActiveX is initialized by the guarded preflight.

(setq *A2FLT-StyleName* "Mr.T_BAHNSCHRIFT")
(setq *A2FLT-Typeface*  "Bahnschrift")
(setq *A2FLT-FontFileName* "bahnschrift.ttf")
(setq *A2FLT-PitchFamily* 34) ; VARIABLE_PITCH(2) + FF_SWISS(32)
(setq *MRTFONT-Preselection* nil)

;; ================= LT2024 RUN CACHE CACHE =================
(setq *A2FLT-StyleCache* nil)
(setq *A2FLT-StylesCollection* nil)
(setq *A2FLT-TargetStyleReady* nil)
(setq *A2FLT-TargetStyleObject* nil)

(setq *A2FLT-StatStyleHits* 0)
(setq *A2FLT-StatStyleMisses* 0)
(setq *A2FLT-StatContentScans* 0)

(defun A2FLT:ResetRunCaches (doc)
  (setq *A2FLT-StyleCache* nil)

  (setq *A2FLT-StylesCollection*
    (vl-catch-all-apply 'vla-get-TextStyles (list doc)))
  (if (vl-catch-all-error-p *A2FLT-StylesCollection*)
    (setq *A2FLT-StylesCollection* nil)
  )

  (setq *A2FLT-TargetStyleReady* nil)
  (setq *A2FLT-TargetStyleObject* nil)

  (setq *A2FLT-StatStyleHits* 0)
  (setq *A2FLT-StatStyleMisses* 0)
  (setq *A2FLT-StatContentScans* 0)
  (princ)
)

(defun A2FLT:ElapsedSeconds (startMs endMs / d)
  (setq d (- endMs startMs))
  ;; MILLISECS can wrap after a very long Windows session.
  (if (< d 0)
    (setq d (+ d 2147483648))
  )
  (/ d 1000.0)
)


;;; ---------- TCVN3 -> Unicode mapping ----------
;;; TCVN3/ABC uses the same legacy byte positions for lowercase Vietnamese.
;;; Fonts ending in H display those positions as uppercase; uppercase conversion
;;; is handled after mapping, per visible character.

(setq *A2FLT-Map*
  (list
    (cons "µ" "à") (cons "¸" "á") (cons "¶" "ả") (cons "·" "ã") (cons "¹" "ạ")
    (cons "¨" "ă") (cons "»" "ằ") (cons "¾" "ắ") (cons "¼" "ẳ") (cons "½" "ẵ") (cons "Æ" "ặ")
    (cons "©" "â") (cons "Ç" "ầ") (cons "Ê" "ấ") (cons "È" "ẩ") (cons "É" "ẫ") (cons "Ë" "ậ")

    (cons "Ì" "è") (cons "Ð" "é") (cons "Î" "ẻ") (cons "Ï" "ẽ") (cons "Ñ" "ẹ")
    (cons "ª" "ê") (cons "Ò" "ề") (cons "Õ" "ế") (cons "Ó" "ể") (cons "Ô" "ễ") (cons "Ö" "ệ")

    (cons "×" "ì") (cons "Ý" "í") (cons "Ø" "ỉ") (cons "Ü" "ĩ") (cons "Þ" "ị")

    (cons "ß" "ò") (cons "ã" "ó") (cons "á" "ỏ") (cons "â" "õ") (cons "ä" "ọ")
    (cons "«" "ô") (cons "å" "ồ") (cons "è" "ố") (cons "æ" "ổ") (cons "ç" "ỗ") (cons "é" "ộ")
    (cons "¬" "ơ") (cons "ê" "ờ") (cons "í" "ớ") (cons "ë" "ở") (cons "ì" "ỡ") (cons "î" "ợ")

    (cons "ï" "ù") (cons "ó" "ú") (cons "ñ" "ủ") (cons "ò" "ũ") (cons "ô" "ụ")
    (cons "­" "ư") (cons "õ" "ừ") (cons "ø" "ứ") (cons "ö" "ử") (cons "÷" "ữ") (cons "ù" "ự")

    (cons "ú" "ỳ") (cons "ý" "ý") (cons "û" "ỷ") (cons "ü" "ỹ") (cons "þ" "ỵ")
    (cons "®" "đ")

    ;; Base uppercase letters explicitly present in TCVN3
    (cons "¡" "Ă") (cons "¢" "Â") (cons "£" "Ê")
    (cons "¤" "Ô") (cons "¥" "Ơ") (cons "¦" "Ư") (cons "§" "Đ")
  )
)

(defun A2FLT:Map ()
  *A2FLT-Map*
)

(defun A2FLT:ContainsCI (s sub)
  (and s sub
       (vl-string-search (strcase sub T) (strcase s T)))
)

(defun A2FLT:UnicodeReadyP (/ v)
  (setq v (vl-catch-all-apply 'getvar (list "LISPSYS")))
  (and (not (vl-catch-all-error-p v))
       (/= v 0))
)

(defun A2FLT:FontPath (/ windir localdir p)
  (setq windir (getenv "WINDIR") localdir (getenv "LOCALAPPDATA"))
  (cond
    ((setq p (findfile *A2FLT-FontFileName*)) p)
    ((and windir (setq p (findfile
       (strcat windir "\\Fonts\\" *A2FLT-FontFileName*)))) p)
    ((and localdir (setq p (findfile (strcat localdir
       "\\Microsoft\\Windows\\Fonts\\" *A2FLT-FontFileName*)))) p)
    (T nil)))

(defun A2FLT:SetTargetBahnschrift ()
  (setq *A2FLT-StyleName* "Mr.T_BAHNSCHRIFT")
  (setq *A2FLT-Typeface* "Bahnschrift")
  (setq *A2FLT-FontFileName* "bahnschrift.ttf")
  (setq *A2FLT-PitchFamily* 34) ; VARIABLE_PITCH + FF_SWISS
)

(defun A2FLT:SetTargetArialNarrow ()
  (setq *A2FLT-StyleName* "Mr.T_ARIAL_NARROW")
  (setq *A2FLT-Typeface* "Arial Narrow")
  (setq *A2FLT-FontFileName* "arialn.ttf")
  (setq *A2FLT-PitchFamily* 34) ; VARIABLE_PITCH + FF_SWISS
)

(defun A2FLT:SetTargetArial ()
  (setq *A2FLT-StyleName* "Mr.T_ARIAL")
  (setq *A2FLT-Typeface* "Arial")
  (setq *A2FLT-FontFileName* "arial.ttf")
  (setq *A2FLT-PitchFamily* 34) ; VARIABLE_PITCH + FF_SWISS
)

(defun A2FLT:SetTargetTimesNewRoman ()
  (setq *A2FLT-StyleName* "Mr.T_TIMES_NEW_ROMAN")
  (setq *A2FLT-Typeface* "Times New Roman")
  (setq *A2FLT-FontFileName* "times.ttf")
  (setq *A2FLT-PitchFamily* 18) ; VARIABLE_PITCH(2) + FF_ROMAN(16)
)

(defun A2FLT:WarnArialNarrow ()
  (alert "Arial Narrow: kiểm tra đủ dấu tiếng Việt trên máy đang dùng.\nChạy thử ABC2ARIALN_SEL trước khi chuyển toàn bộ bản vẽ.")
  (princ))


(defun A2FLT:GetStyleFontInfo (sty / tf bd it cs pf ff r)
  ;; Returns (Typeface FontFile). Empty strings if unavailable.
  (setq tf "" ff "")
  (if sty
    (progn
      (setq r (vl-catch-all-apply 'vla-GetFont
                (list sty 'tf 'bd 'it 'cs 'pf)))
      (if (vl-catch-all-error-p r) (setq tf ""))

      (setq r (vl-catch-all-apply 'vla-get-FontFile (list sty)))
      (if (vl-catch-all-error-p r)
        (setq ff "")
        (setq ff r)
      )
    )
  )
  (list tf ff)
)

(defun A2FLT:TargetStyleP (sty / fi)
  ;; Reuse only a neutral, correctly configured style. Never edit old styles.
  (and sty
    (setq fi (A2FLT:GetStyleFontInfo sty))
    (or (= (strcase (car fi)) (strcase *A2FLT-Typeface*))
        (and (/= (cadr fi) "") (vl-filename-base (cadr fi))
             (= (strcase (vl-filename-base (cadr fi)))
                (strcase (vl-filename-base *A2FLT-FontFileName*)))))
    (= (vla-get-Height sty) 0.0)
    (= (vla-get-Width sty) 1.0)
    (= (vla-get-ObliqueAngle sty) 0.0)
    (= (vla-get-TextGenerationFlag sty) 0)))

(defun A2FLT:StyleExists (styles name / r)
  (setq r (vl-catch-all-apply 'vla-Item (list styles name)))
  (if (vl-catch-all-error-p r) nil r)
)

(defun A2FLT:ResolveTargetStyleName (doc baseName / styles sty name i)
  ;; IMPORTANT:
  ;; Never overwrite a pre-existing style with the same name if it points to
  ;; another font. That style may be the TCVN3 source style (e.g. ARIAL ->
  ;; .VnArial), and changing it before Auto detection makes Auto fail.
  ;;
  ;; Preferred target name remains clean: ARIAL / BAHNSCHRIFT / ...
  ;; Only on collision do we use *_UNICODE, *_UNICODE_2, ...
  (setq styles (vla-get-TextStyles doc))
  (setq sty (A2FLT:StyleExists styles baseName))

  (cond
    ((not sty)
      baseName
    )
    ((A2FLT:TargetStyleP sty)
      baseName
    )
    (T
      (setq name (strcat baseName "_UNICODE"))
      (setq i 2)
      (while
        (progn
          (setq sty (A2FLT:StyleExists styles name))
          (and sty (not (A2FLT:TargetStyleP sty)))
        )
        (setq name (strcat baseName "_UNICODE_" (itoa i)))
        (setq i (1+ i))
      )
      name
    )
  )
)

(defun A2FLT:EnsureStyle (doc / styles sty fp resolvedName)
  (if *A2FLT-TargetStyleReady*
    *A2FLT-TargetStyleObject*
    (progn
      (setq styles *A2FLT-StylesCollection*)
      (setq resolvedName (A2FLT:ResolveTargetStyleName doc *A2FLT-StyleName*))
      (setq sty (A2FLT:StyleExists styles resolvedName))
      (if (not sty)
        (progn
          (setq fp (A2FLT:FontPath))
          (setq sty (vla-Add styles resolvedName))
          (vla-put-FontFile sty fp)
          (vla-SetFont sty *A2FLT-Typeface* :vlax-false :vlax-false
                       0 *A2FLT-PitchFamily*)
          (vla-put-Height sty 0.0)
          (vla-put-Width sty 1.0)
          (vla-put-ObliqueAngle sty 0.0)
          (vla-put-TextGenerationFlag sty 0)))
      (setq *A2FLT-StyleName* resolvedName
            *A2FLT-TargetStyleObject* sty
            *A2FLT-TargetStyleReady* T)
      sty)))

(defun A2FLT:StyleObject (doc styleName / styles sty)
  (if (and styleName (/= styleName ""))
    (progn
      (setq styles
        (if *A2FLT-StylesCollection*
          *A2FLT-StylesCollection*
          (vla-get-TextStyles doc)
        )
      )
      (setq sty
        (vl-catch-all-apply 'vla-Item
          (list styles styleName)))
      (if (vl-catch-all-error-p sty) nil sty)
    )
  )
)

(defun A2FLT:EndsHFontP (s / x n)
  ;; Case-normalize before testing extension; fixes .VnTimeH.ttf detection.
  (if (and s (/= s ""))
    (progn
      (setq x (strcase s))
      (if (wcmatch x "*.TTF,*.OTF,*.SHX")
        (setq x (substr x 1 (- (strlen x) 4))))
      (setq n (strlen x))
      (and (> n 0) (= (substr x n 1) "H")))
    nil))


(defun A2FLT:StyleTableFontInfo (styleName / rec ff bf)
  ;; Read the STYLE symbol table directly from the DWG.
  ;; Group 3 = primary font file, group 4 = bigfont file.
  ;; This is often more reliable for old TCVN3 drawings than ActiveX GetFont.
  (setq ff "" bf "")
  (if (and styleName (/= styleName ""))
    (progn
      (setq rec (tblsearch "STYLE" styleName))
      (if rec
        (progn
          (if (assoc 3 rec) (setq ff (cdr (assoc 3 rec))))
          (if (assoc 4 rec) (setq bf (cdr (assoc 4 rec))))
        )
      )
    )
  )
  (list ff bf)
)

(defun A2FLT:ABCSignatureP (sig / s)
  ;; Common TCVN3/ABC family/style/file signatures.
  ;; Include legacy Windows font-file naming variants as well.
  (setq s (if sig (strcase sig T) ""))
  (or
    (vl-string-search ".vn" s)
    (vl-string-search "vntime" s)
    (vl-string-search "vnarial" s)
    (vl-string-search "vntimeh" s)
    (vl-string-search "vnarialh" s)
    (vl-string-search "vharial" s)
    (vl-string-search "vhtime" s)
    (vl-string-search "tcvn3" s)
    (vl-string-search "tcvn-3" s)
    (vl-string-search "abc font" s)
  )
)

(defun A2FLT:ABCHSignatureP (sig / s)
  ;; Detect uppercase TCVN3 "H" font families.
  (setq s (if sig (strcase sig T) ""))
  (or
    (vl-string-search "vntimeh" s)
    (vl-string-search "vnarialh" s)
    (vl-string-search ".vntimeh" s)
    (vl-string-search ".vnarialh" s)
    (vl-string-search "vhtime" s)
    (vl-string-search "vharial" s)
  )
)

(setq *A2FLT-StrongLegacyChars*
  (list
    "µ" "¸" "¶" "·" "¹" "¨" "»" "¾" "¼" "½" "Æ" "©"
    "Ç" "È" "É" "Ë" "Ì" "Ð" "Î" "Ï" "Ñ" "ª" "Ò" "Õ"
    "Ó" "Ô" "Ö" "×" "Ø" "Ü" "Þ" "ß" "«" "å" "æ" "ç"
    "¬" "î" "ï" "ñ" "­" "ø" "÷" "þ" "®" "¡" "¢" "£"
    "¤" "¥" "¦" "§"
  )
)

(defun A2FLT:StrongLegacyChars ()
  *A2FLT-StrongLegacyChars*
)

(defun A2FLT:LegacyCharWeight (ch /)
  (cond
    ((member ch *A2FLT-StrongLegacyChars*) 2)
    ((assoc ch *A2FLT-Map*) 1)
    (T 0)
  )
)

(defun A2FLT:LegacyContentScore (s / i n score)
  ;; Full score for ABC2CHECK diagnostics.
  (setq i 1)
  (setq n (strlen (if s s "")))
  (setq score 0)

  (while (<= i n)
    (setq score
      (+ score
        (A2FLT:LegacyCharWeight
          (substr s i 1))))
    (setq i (1+ i))
  )
  score
)

(defun A2FLT:LegacyContentPFast (s / i n score)
  ;; AUTO needs only true/false, so stop as soon as score reaches 2.
  (setq *A2FLT-StatContentScans*
    (1+ *A2FLT-StatContentScans*))

  (setq i 1)
  (setq n (strlen (if s s "")))
  (setq score 0)

  (while (and (<= i n) (< score 2))
    (setq score
      (+ score
        (A2FLT:LegacyCharWeight
          (substr s i 1))))
    (setq i (1+ i))
  )
  (>= score 2)
)

(defun A2FLT:LegacyContentP (s /)
  (A2FLT:LegacyContentPFast s)
)



(defun A2FLT:StyleInfo (doc styleName / sty fi ti tf ff bf sig isABC isH)
  ;; Use actual font metadata, not a potentially misleading style name.
  (setq sty (A2FLT:StyleObject doc styleName)
        fi (A2FLT:GetStyleFontInfo sty)
        ti (A2FLT:StyleTableFontInfo styleName)
        tf (car fi) ff (cadr fi) bf (cadr ti))
  (setq sig (strcat tf "|" ff "|" (car ti) "|" bf))
  (if (= sig "|||") (setq sig (if styleName styleName "")))
  (setq isABC (A2FLT:ABCSignatureP sig))
  (setq isH (and isABC
    (or (A2FLT:ABCHSignatureP sig) (A2FLT:EndsHFontP tf)
        (A2FLT:EndsHFontP ff) (A2FLT:EndsHFontP (car ti))
        (A2FLT:EndsHFontP bf))))
  (list isABC isH sig))

(defun A2FLT:StyleInfoCached (doc styleName / key hit info)
  ;; A style may be used by tens of thousands of text objects.
  ;; ActiveX + STYLE table are read only once per style per command run.
  (setq key
    (strcase (if styleName styleName "")))
  (setq hit (assoc key *A2FLT-StyleCache*))

  (if hit
    (progn
      (setq *A2FLT-StatStyleHits*
        (1+ *A2FLT-StatStyleHits*))
      (cdr hit)
    )
    (progn
      (setq *A2FLT-StatStyleMisses*
        (1+ *A2FLT-StatStyleMisses*))
      (setq info (A2FLT:StyleInfo doc styleName))
      (setq *A2FLT-StyleCache*
        (cons (cons key info)
          *A2FLT-StyleCache*))
      info
    )
  )
)

;; Replaced by the LT-safe batch processor.

(defun A2FLT:ObjectStyleName (obj / r)
  (cond
    ((vlax-property-available-p obj 'TextStyleName)
      (setq r (vl-catch-all-apply 'vla-get-TextStyleName (list obj)))
      (if (vl-catch-all-error-p r) "" r)
    )
    ((vlax-property-available-p obj 'StyleName)
      (setq r (vl-catch-all-apply 'vla-get-StyleName (list obj)))
      (if (vl-catch-all-error-p r) "" r)
    )
    (T "")
  )
)

(defun A2FLT:SetObjectStyle (obj name)
  ;; Fail explicitly if style write fails; caller restores the original text.
  (if (= (vla-get-ObjectName obj) "AcDbMLeader")
    (vla-put-TextStyleName obj name)
    (vla-put-StyleName obj name)))

(defun A2FLT:ReplaceAll (s old new / p)
  ;; Replace every occurrence of OLD with NEW.
  (if (and s old new (/= old ""))
    (while (setq p (vl-string-search old s))
      (setq s
        (strcat
          (substr s 1 p)
          new
          (substr s (+ p (strlen old) 1))
        )
      )
    )
  )
  s
)

(defun A2FLT:NormalizeForTarget (s /)
  ;; AutoCAD + Bahnschrift on some systems has been observed to render
  ;; precomposed U+1EF7/U+1EF6 (ỷ/Ỷ) incorrectly.
  ;; Use canonically equivalent decomposed Unicode:
  ;;   ỷ = y + U+0309 COMBINING HOOK ABOVE
  ;;   Ỷ = Y + U+0309 COMBINING HOOK ABOVE
  ;; Arial is kept as the baseline/reference path and is not modified.
  (if (= (strcase *A2FLT-Typeface*) "BAHNSCHRIFT")
    (progn
      (setq s (A2FLT:ReplaceAll s "ỷ" "ỷ"))
      (setq s (A2FLT:ReplaceAll s "Ỷ" "Ỷ"))
    )
  )
  s
)

(defun A2FLT:MapChar (ch makeUpper / a out up)
  (setq a (assoc ch *A2FLT-Map*) out (if a (cdr a) ch))
  (if makeUpper
    (if (setq up (assoc out *A2FLT-UpperMap*)) (cdr up) (strcase out))
    out))

(defun A2FLT:MTextArgCodeP (c)
  ;; Paragraph formatting (lowercase p) is case-sensitive; preserve its payload.
  (if (member c '("F" "f" "H" "h" "W" "w" "T" "t"
                  "Q" "q" "C" "c" "A" "a" "S" "s" "p")) T nil))

(defun A2FLT:ConvertString (s baseABC makeUpper isMText forceMode /
  i n out ch c2 end token family ctx stack active upper fieldEnd converted)
  ;; Scope stack protects Unicode runs inside mixed-font MTEXT.
  ;; Nested fields, escaped braces, %% symbols and formatting remain intact.
  (setq i 1 n (strlen s) out "" ctx (list baseABC makeUpper)
        stack nil converted nil)
  (while (<= i n)
    (setq ch (substr s i 1))
    (cond
      ((= (substr s i 2) "%<")
        (setq fieldEnd (A2FLT:FieldEnd s i))
        (setq out (strcat out (substr s i (- fieldEnd i))))
        (setq i fieldEnd))
      ((= (substr s i 2) "%%")
        (setq end (+ i 3))
        (if (and (<= (+ i 4) n)
                 (wcmatch (substr s (+ i 2) 3) "###"))
          (setq end (+ i 5)))
        (setq out (strcat out (substr s i (- end i))))
        (setq i end))
      ((and isMText (= ch "{"))
        (setq stack (cons ctx stack) out (strcat out ch) i (1+ i)))
      ((and isMText (= ch "}"))
        (if stack (setq ctx (car stack) stack (cdr stack)))
        (setq out (strcat out ch) i (1+ i)))
      ((and (= ch "\\") (< i n))
        (setq c2 (substr s (1+ i) 1))
        (cond
          ;; Preserve an existing Unicode escape, without changing its hex digits.
          ((and (= (strcase c2) "U") (= (substr s (+ i 2) 1) "+")
                (<= (+ i 6) n))
            (setq out (strcat out (substr s i 7)) i (+ i 7)))
          ((and isMText (member c2 '("f" "F"))
                (setq end (vl-string-search ";" s i)))
            (setq token (substr s (+ i 2) (- end i 1)))
            (setq family (A2FLT:FontFamily token))
            (setq active (or forceMode (A2FLT:ABCSignatureP family)))
            (setq upper (if forceMode (= forceMode "H")
              (and active (or (A2FLT:EndsHFontP family)
                              (A2FLT:ABCHSignatureP family)))))
            (setq ctx (list active upper))
            (setq out (strcat out
              (if active
                (strcat "\\f" (A2FLT:TargetFontToken token) ";")
                (substr s i (- (+ end 2) i)))))
            (if active (setq converted T))
            (setq i (+ end 2)))
          ((and isMText (member c2 '("S" "s"))
                (setq end (vl-string-search ";" s i)))
            ;; Stacked text is visible content, so its letters need conversion.
            (setq token (substr s (+ i 2) (- end i 1)))
            (setq out (strcat out ch c2
              (A2FLT:ConvertString token (car ctx) (cadr ctx) nil forceMode) ";"))
            (setq i (+ end 2)))
          ((and isMText (A2FLT:MTextArgCodeP c2)
                (setq end (vl-string-search ";" s i)))
            (setq out (strcat out (substr s i (- (+ end 2) i)))
                  i (+ end 2)))
          (T (setq out (strcat out ch c2) i (+ i 2)))))
      (T
        (setq out (strcat out
          (if (car ctx) (A2FLT:NormalizeForTarget (A2FLT:MapChar ch (cadr ctx))) ch)))
        (if (car ctx) (setq converted T))
        (setq i (1+ i)))))
  out)

(defun A2FLT:HasInlineFontP (raw)
  (and raw
    (or
      (vl-string-search "\\f" raw)
      (vl-string-search "\\F" raw)
    )
  )
)

(defun A2FLT:InlineABCInfo (raw / i n ch c2 end token family isABC isH)
  ;; Parse actual font controls. Ordinary text containing VNTIME is not a font.
  (setq i 1 n (strlen (if raw raw "")))
  (while (<= i n)
    (setq ch (substr raw i 1))
    (cond
      ((and (= ch "\\") (< i n))
        (setq c2 (substr raw (1+ i) 1))
        (if (and (member c2 '("f" "F"))
                 (setq end (vl-string-search ";" raw i)))
          (progn
            (setq token (substr raw (+ i 2) (- end i 1)))
            (setq family (A2FLT:FontFamily token))
            (if (A2FLT:ABCSignatureP family)
              (setq isABC T isH (or isH (A2FLT:EndsHFontP family)
                                         (A2FLT:ABCHSignatureP family))))
            (setq i (+ end 2)))
          (setq i (+ i 2))))
      ((= (substr raw i 2) "%<")
        (setq i (A2FLT:FieldEnd raw i)))
      (T (setq i (1+ i)))))
  (list isABC isH))

(defun A2FLT:TextObjectNameP (nm)
  (if
    (member nm
      '("AcDbText"
        "AcDbMText"
        "AcDbAttribute"
        "AcDbAttributeDefinition"
        "AcDbMLeader"))
    T nil
  )
)

(defun A2FLT:TextBearingP (obj / nm)
  (setq nm
    (vl-catch-all-apply 'vla-get-ObjectName
      (list obj)))
  (and
    (not (vl-catch-all-error-p nm))
    (A2FLT:TextObjectNameP nm)
  )
)

(defun A2FLT:IsMTextLikeP (obj / nm)
  (setq nm (vla-get-ObjectName obj))
  (or (= nm "AcDbMText")
      (= nm "AcDbMLeader"))
)

(defun A2FLT:ProcessTextObject (doc obj forceMode /
  raw styleName sinfo iinfo isABC isH isMText new objName layer r)
  (setq objName (vla-get-ObjectName obj))
  (cond
    ((not (A2FLT:TextObjectNameP objName)) 0)
    ((and (= objName "AcDbMLeader") (/= (vla-get-ContentType obj) 2)) 0)
    ((progn
       (setq layer (tblsearch "LAYER" (vla-get-Layer obj)))
       (and layer (/= 0 (logand 4 (cdr (assoc 70 layer))))))
      (setq *A2FLT-Locked* (1+ *A2FLT-Locked*)) 0)
    (T
      (setq raw (vla-get-TextString obj)
            styleName (A2FLT:ObjectStyleName obj)
            isMText (member objName '("AcDbMText" "AcDbMLeader")))
      ;; Multiline attributes expose additional embedded MTEXT. Do not flatten it.
      (if (and (member objName '("AcDbAttribute" "AcDbAttributeDefinition"))
               (vlax-property-available-p obj 'MTextAttribute)
               (= (vla-get-MTextAttribute obj) :vlax-true))
        (setq raw (vla-get-MTextAttributeContent obj) isMText T))
      (setq sinfo (A2FLT:StyleInfoCached doc styleName)
            iinfo (if isMText (A2FLT:InlineABCInfo raw) '(nil nil)))
      (setq isABC (or forceMode (car sinfo) (car iinfo)))
      (setq isH (if forceMode (= forceMode "H") (cadr sinfo)))
      (if (not isABC)
        (progn
          (if (A2FLT:LegacyContentPFast raw)
            (setq *A2FLT-Ambiguous* (1+ *A2FLT-Ambiguous*)))
          0)
        (progn
          (A2FLT:EnsureStyle doc)
          (setq new (A2FLT:ConvertString raw (or forceMode (car sinfo))
                                      isH isMText forceMode))
          (setq r (vl-catch-all-apply 'A2FLT:CommitText
                    (list obj new *A2FLT-StyleName*)))
          (if (or (vl-catch-all-error-p r) (not r))
            (progn
              ;; Best effort restoration; never report a failed partial write as success.
              (setq r (vl-catch-all-apply 'A2FLT:CommitText (list obj raw styleName)))
              (if (or (vl-catch-all-error-p r) (not r))
                (prompt "\nKhông phục hồi được đối tượng; dùng UNDO để hoàn tác lượt chạy."))
              -1)
            1))))))

(defun A2FLT:VariantObjectsToList (v / a)
  (if (and v (not (vl-catch-all-error-p v)))
    (progn
      (setq a (vlax-variant-value v))
      (if (= (type a) 'SAFEARRAY)
        (vlax-safearray->list a)
        nil
      )
    )
    nil
  )
)

;; Replaced by the LT-safe batch processor.

(defun A2FLT:ScanAll (doc / blocks blk obj nm r)
  ;; Definitions are shared; a change here affects every reference.
  (setq blocks (vla-get-Blocks doc))
  (vlax-for blk blocks
    (setq nm (vla-get-Name blk))
    (if (and (/= (vla-get-IsXRef blk) :vlax-true)
             (not (vl-string-search "|" nm))
             (not (wcmatch (strcase nm) "`*D*")))
      (vlax-for obj blk
        (A2FLT:CountResult (A2FLT:SafeProcess doc obj nil))
        (if (= (vla-get-ObjectName obj) "AcDbBlockReference")
          (A2FLT:SelectedAttributes doc obj nil)))))
  (list *A2FLT-Converted* *A2FLT-Errors* *A2FLT-Skipped*))

;; Replaced by the LT-safe batch processor.

(defun A2FLT:Preflight (/ r)
  (cond
    ((< (atof (getvar "ACADVER")) 24.3)
      (alert "Bản rebuild này yêu cầu AutoCAD LT 2024 hoặc AutoCAD 2024 trở lên, trên Windows.") nil)
    ;; Test the running engine as well as the variable: changing LISPSYS needs a restart.
    ((or (not (A2FLT:UnicodeReadyP)) (/= (ascii "đ") 273))
      (alert "Đặt LISPSYS = 1, đóng và mở lại AutoCAD LT, rồi APPLOAD lại Lisp.\nBộ máy AutoLISP hiện tại chưa xử lý Unicode đúng.") nil)
    ((not (A2FLT:FontPath))
      (alert (strcat "Không tìm thấy font: " *A2FLT-Typeface*
                    "\nCài font này hoặc chọn Arial rồi chạy lại.")) nil)
    ((progn
       (setq r (vl-catch-all-apply 'vl-load-com nil))
       (vl-catch-all-error-p r))
      (alert "Không khởi tạo được ActiveX nội bộ của AutoCAD LT.\nKiểm tra bản cài AutoCAD LT 2024 trên Windows.") nil)
    (T T)))

(defun A2FLT:RunAllCore () (A2FLT:Execute nil) (princ))

(defun c:ABC2BAHN ()
  (A2FLT:SetTargetBahnschrift)
  (A2FLT:RunAllCore)
  (princ)
)

(defun A2FLT:RunSelCore () (A2FLT:Execute T) (princ))

(defun c:ABC2BAHN_SEL ()
  (A2FLT:SetTargetBahnschrift)
  (A2FLT:RunSelCore)
  (princ)
)

(defun A2FLT:ChooseTarget (/ kw)
  (initget "Bahnschrift Arial ArialNarrow TimesNewRoman")
  (setq kw
    (getkword
      "
Chọn font đích [Bahnschrift/Arial/ArialNarrow/TimesNewRoman] <Bahnschrift>: "
    )
  )
  (cond
    ((equal kw "Arial")
      (A2FLT:SetTargetArial)
    )
    ((equal kw "ArialNarrow")
      (A2FLT:WarnArialNarrow)
      (A2FLT:SetTargetArialNarrow)
    )
    ((equal kw "TimesNewRoman")
      (A2FLT:SetTargetTimesNewRoman)
    )
    (T
      (A2FLT:SetTargetBahnschrift)
    )
  )
)

(defun A2FLT:RunAllWithTarget (target /)
  (cond
    ((equal target "BAHN")   (A2FLT:SetTargetBahnschrift))
    ((equal target "ARIAL")  (A2FLT:SetTargetArial))
    ((equal target "ARIALN") (progn (A2FLT:WarnArialNarrow) (A2FLT:SetTargetArialNarrow)))
    ((equal target "TIMES")  (A2FLT:SetTargetTimesNewRoman))
    (T                   (A2FLT:ChooseTarget))
  )
  (A2FLT:RunAllCore)
)

(defun A2FLT:RunSelWithTarget (target /)
  (cond
    ((equal target "BAHN")   (A2FLT:SetTargetBahnschrift))
    ((equal target "ARIAL")  (A2FLT:SetTargetArial))
    ((equal target "ARIALN") (progn (A2FLT:WarnArialNarrow) (A2FLT:SetTargetArialNarrow)))
    ((equal target "TIMES")  (A2FLT:SetTargetTimesNewRoman))
    (T                   (A2FLT:ChooseTarget))
  )
  (A2FLT:RunSelCore)
)

(defun c:ABC2ARIAL ()
  (A2FLT:RunAllWithTarget "ARIAL")
  (princ)
)

(defun c:ABC2ARIAL_SEL ()
  (A2FLT:RunSelWithTarget "ARIAL")
  (princ)
)

(defun c:ABC2TIMES ()
  (A2FLT:RunAllWithTarget "TIMES")
  (princ)
)

(defun c:ABC2TIMES_SEL ()
  (A2FLT:RunSelWithTarget "TIMES")
  (princ)
)

(defun c:ABC2ARIALN ()
  (A2FLT:RunAllWithTarget "ARIALN")
  (princ)
)

(defun c:ABC2ARIALN_SEL ()
  (A2FLT:RunSelWithTarget "ARIALN")
  (princ)
)

(defun c:ABC2FONT ()
  (A2FLT:RunAllWithTarget nil)
  (princ)
)

(defun c:ABC2FONT_SEL ()
  (A2FLT:RunSelWithTarget nil)
  (princ)
)


(defun A2FLT:BoolVN (v)
  (if v "Có" "Không")
)

(defun c:ABC2CHECK (/ doc en obj raw sn si ii msg mt)
  (if (A2FLT:Preflight)
    (progn
      (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
      (setq en (car (nentsel "\nChọn một đối tượng chữ để kiểm tra: ")))
      (if en
        (progn
          (setq obj (vlax-ename->vla-object en))
          (if (A2FLT:TextBearingP obj)
            (progn
              (setq raw (vla-get-TextString obj) sn (A2FLT:ObjectStyleName obj)
                    si (A2FLT:StyleInfo doc sn) mt (A2FLT:IsMTextLikeP obj))
              (if (and (vlax-property-available-p obj 'MTextAttribute)
                       (= (vla-get-MTextAttribute obj) :vlax-true))
                (setq raw (vla-get-MTextAttributeContent obj) mt T))
              (setq ii (if mt (A2FLT:InlineABCInfo raw) '(nil nil)))
              (alert (strcat "ABC2CHECK | LT 2024\n\nStyle: " sn
                "\nFont lưu trong DWG: " (caddr si)
                "\nTCVN3 từ style: " (A2FLT:BoolVN (car si))
                "\nFont H từ style: " (A2FLT:BoolVN (cadr si))
                "\nTCVN3 từ font nội tuyến: " (A2FLT:BoolVN (car ii))
                "\n\nNhận dạng mã chữ: "
                (if (or (car si) (car ii)) "TCVN3/ABC"
                  "Chưa xác định TCVN3; Auto bỏ qua.")
                "\nLayer khóa hoặc MLEADER không có MTEXT sẽ bỏ qua.\nNếu đã xác nhận là ABC: dùng _SEL > Thuong hoặc Hoa.")))
            (alert "Chọn TEXT, MTEXT, ATTRIB, ATTDEF hoặc MLEADER có chữ."))))))
  (princ))



(defun A2FLT:FontFamily (token / p)
  (if (setq p (vl-string-search "|" token)) (substr token 1 p) token))

(defun A2FLT:TargetFontToken (token / p suffix q)
  (setq suffix (if (setq p (vl-string-search "|" token)) (substr token (1+ p)) ""))
  ;; Keep bold/italic while resetting the legacy font's charset/pitch metadata.
  (while (setq p (vl-string-search "|c" suffix))
    (setq q (vl-string-search "|" suffix (1+ p)))
    (setq suffix (strcat (substr suffix 1 p) (if q (substr suffix (1+ q)) ""))))
  (while (setq p (vl-string-search "|p" suffix))
    (setq q (vl-string-search "|" suffix (1+ p)))
    (setq suffix (strcat (substr suffix 1 p) (if q (substr suffix (1+ q)) ""))))
  (strcat *A2FLT-Typeface* suffix "|c0|p" (itoa *A2FLT-PitchFamily*)))

(defun A2FLT:FieldEnd (s i / depth n)
  ;; Return the one-based position after a balanced (possibly nested) field.
  (setq depth 1 n (strlen s) i (+ i 2))
  (while (and (<= i n) (> depth 0))
    (cond
      ((= (substr s i 2) "%<") (setq depth (1+ depth) i (+ i 2)))
      ((= (substr s i 2) ">%") (setq depth (1- depth) i (+ i 2)))
      (T (setq i (1+ i)))))
  (if (> depth 0) (1+ n) i))

(defun A2FLT:CommitText (obj text style / nm written)
  (setq nm (vla-get-ObjectName obj))
  (A2FLT:SetObjectStyle obj style)
  (if (and (member nm '("AcDbAttribute" "AcDbAttributeDefinition"))
           (vlax-property-available-p obj 'MTextAttribute)
           (= (vla-get-MTextAttribute obj) :vlax-true))
    (progn (vla-put-MTextAttributeContent obj text) (vla-UpdateMTextAttribute obj))
    (vla-put-TextString obj text))
  ;; Read back both writes; never silently count a style failure as converted.
  (setq written
    (if (and (member nm '("AcDbAttribute" "AcDbAttributeDefinition"))
             (vlax-property-available-p obj 'MTextAttribute)
             (= (vla-get-MTextAttribute obj) :vlax-true))
      (vla-get-MTextAttributeContent obj)
      (vla-get-TextString obj)))
  (and (= (strcase (A2FLT:ObjectStyleName obj)) (strcase style))
       (= written text)))

(defun A2FLT:SafeProcess (doc obj mode / r nm)
  (setq nm (vla-get-ObjectName obj))
  (if (A2FLT:TextObjectNameP nm)
    (progn
      (setq r (vl-catch-all-apply 'A2FLT:ProcessTextObject (list doc obj mode)))
      (if (vl-catch-all-error-p r)
        (progn
          (prompt (strcat "\nHandle " (vla-get-Handle obj) ": "
                          (vl-catch-all-error-message r))) -1)
        r))
    nil))

(defun A2FLT:CountResult (r)
  (cond ((equal r 1) (setq *A2FLT-Converted* (1+ *A2FLT-Converted*)))
        ((equal r -1) (setq *A2FLT-Errors* (1+ *A2FLT-Errors*)))
        ((equal r 0) (setq *A2FLT-Skipped* (1+ *A2FLT-Skipped*))))
  (princ))

(defun A2FLT:SelectedAttributes (doc obj mode / v atts a)
  (if (and (= (vla-get-ObjectName obj) "AcDbBlockReference")
           (= (vla-get-HasAttributes obj) :vlax-true)
           (not (vl-string-search "|" (vla-get-Name obj)))
           (= 0 (logand 28 (cdr (assoc 70 (tblsearch "BLOCK" (vla-get-Name obj)))))))
    (progn
      (setq v (vl-catch-all-apply 'vla-GetAttributes (list obj)))
      (if (vl-catch-all-error-p v)
        (setq *A2FLT-Errors* (1+ *A2FLT-Errors*))
        (progn
          (setq atts (A2FLT:VariantObjectsToList v))
          (foreach a atts (A2FLT:CountResult (A2FLT:SafeProcess doc a mode)))))))
  (princ))

(defun A2FLT:Execute (selected /
  *error* doc undoOpen ss kw mode i obj startMs oldEcho errEnd pre confirm)
  (defun *error* (msg)
    (if undoOpen
      (vl-catch-all-apply 'vla-EndUndoMark (list doc)))
    (if oldEcho (setvar "CMDECHO" oldEcho))
    (if (and msg (not (wcmatch (strcase msg) "*CANCEL*,*QUIT*,*EXIT*")))
      (prompt (strcat "\nABC2FONT: " msg)))
    (prompt "\nĐã dừng. Dùng UNDO để hoàn tác lượt chuyển này.")
    (princ))
  (if (A2FLT:Preflight)
    (progn
      (setq doc (vla-get-ActiveDocument (vlax-get-acad-object)))
      (if selected
        (progn
          (initget "Auto Thuong Hoa")
          (setq kw (getkword "\nChế độ [Auto/Thuong/Hoa] <Auto> (ép Thuong/Hoa có thể làm hỏng Unicode): "))
          (setq mode (cond ((equal kw "Thuong") "R") ((equal kw "Hoa") "H") (T nil)))
          (prompt "\nChọn chữ hoặc block có attribute. _SEL không sửa chữ trong định nghĩa block: ")
          (setq ss (or *MRTFONT-Preselection* (ssget '((0 . "TEXT,MTEXT,ATTDEF,ATTRIB,MULTILEADER,INSERT")))))
          (setq *MRTFONT-Preselection* nil))
        (progn
          (prompt "\nChuyển Auto: Model, Layout và định nghĩa block dùng chung.")
          (initget "Yes No")
          (setq confirm (getkword "\nCẢNH BÁO: có thể ảnh hưởng chữ Unicode và block động dùng chung. Chỉ thử trên bản sao DWG. Tiếp tục toàn bản vẽ? [Yes/No] <No>: "))))
      (if (or (and (not selected) (equal confirm "Yes")) (and selected ss))
        (progn
          (A2FLT:ResetRunCaches doc)
          (setq *A2FLT-Converted* 0 *A2FLT-Errors* 0 *A2FLT-Skipped* 0
                *A2FLT-Locked* 0 *A2FLT-Ambiguous* 0)
          (setq startMs (getvar "MILLISECS") oldEcho (getvar "CMDECHO"))
          (setvar "CMDECHO" 0)
          (vla-StartUndoMark doc) (setq undoOpen T)
          (if selected
            (progn
              (setq i 0)
              (repeat (sslength ss)
                (setq obj (vlax-ename->vla-object (ssname ss i)))
                (A2FLT:CountResult (A2FLT:SafeProcess doc obj mode))
                (A2FLT:SelectedAttributes doc obj mode)
                (setq i (1+ i))))
            (A2FLT:ScanAll doc))
          (vla-EndUndoMark doc) (setq undoOpen nil)
          (vla-Regen doc 1)
          (setvar "CMDECHO" oldEcho)
          (prompt (strcat "\nMr.T Đổi font | Hoàn tất"
            "\n  Đã chuyển: " (itoa *A2FLT-Converted*)
            " | Bỏ qua: " (itoa *A2FLT-Skipped*)
            " | Lỗi: " (itoa *A2FLT-Errors*)
            "\n  Layer khóa: " (itoa *A2FLT-Locked*)
            " | Chưa rõ bảng mã: " (itoa *A2FLT-Ambiguous*)
            "\n  Font: " *A2FLT-Typeface*
            " | Thời gian: " (rtos (A2FLT:ElapsedSeconds startMs (getvar "MILLISECS")) 2 2) " s"
            "\n  UNDO: hoàn tác lượt chuyển. ABC2HELP: xem hướng dẫn.")))
        (prompt "\nKhông có đối tượng được chọn."))))
  (princ))
(setq *A2FLT-UpperMap* (list
  (cons "à" "À")
  (cons "á" "Á")
  (cons "ả" "Ả")
  (cons "ã" "Ã")
  (cons "ạ" "Ạ")
  (cons "ă" "Ă")
  (cons "ằ" "Ằ")
  (cons "ắ" "Ắ")
  (cons "ẳ" "Ẳ")
  (cons "ẵ" "Ẵ")
  (cons "ặ" "Ặ")
  (cons "â" "Â")
  (cons "ầ" "Ầ")
  (cons "ấ" "Ấ")
  (cons "ẩ" "Ẩ")
  (cons "ẫ" "Ẫ")
  (cons "ậ" "Ậ")
  (cons "è" "È")
  (cons "é" "É")
  (cons "ẻ" "Ẻ")
  (cons "ẽ" "Ẽ")
  (cons "ẹ" "Ẹ")
  (cons "ê" "Ê")
  (cons "ề" "Ề")
  (cons "ế" "Ế")
  (cons "ể" "Ể")
  (cons "ễ" "Ễ")
  (cons "ệ" "Ệ")
  (cons "ì" "Ì")
  (cons "í" "Í")
  (cons "ỉ" "Ỉ")
  (cons "ĩ" "Ĩ")
  (cons "ị" "Ị")
  (cons "ò" "Ò")
  (cons "ó" "Ó")
  (cons "ỏ" "Ỏ")
  (cons "õ" "Õ")
  (cons "ọ" "Ọ")
  (cons "ô" "Ô")
  (cons "ồ" "Ồ")
  (cons "ố" "Ố")
  (cons "ổ" "Ổ")
  (cons "ỗ" "Ỗ")
  (cons "ộ" "Ộ")
  (cons "ơ" "Ơ")
  (cons "ờ" "Ờ")
  (cons "ớ" "Ớ")
  (cons "ở" "Ở")
  (cons "ỡ" "Ỡ")
  (cons "ợ" "Ợ")
  (cons "ù" "Ù")
  (cons "ú" "Ú")
  (cons "ủ" "Ủ")
  (cons "ũ" "Ũ")
  (cons "ụ" "Ụ")
  (cons "ư" "Ư")
  (cons "ừ" "Ừ")
  (cons "ứ" "Ứ")
  (cons "ử" "Ử")
  (cons "ữ" "Ữ")
  (cons "ự" "Ự")
  (cons "ỳ" "Ỳ")
  (cons "ý" "Ý")
  (cons "ỷ" "Ỷ")
  (cons "ỹ" "Ỹ")
  (cons "ỵ" "Ỵ")
  (cons "đ" "Đ")
))

(setq *A2FLT-NoticeAssets* (list
 (list 480 257 (strcat
  "!7!9!'!\"!!F!9!,!\"!!^!9!(!\"!!k!9!(!\"!!y!9!-!\"!\"0!9!'!\"!\"?!9!&!\"!\"M!9!%!\"!\"S!9!1!\"!!7!:!'!\"!!F!:!.!\"!!\\!:!+!\"!!i!:!+!\"!!y!:!-!\"!\"-!:!,!\"!\"?!:!'!\"!\"M!:!%!\"!\"S!:!1!\"!!6!;!(!\"!!F!;!/!\"!!Z!;!-!\"!!i!;!,!\"!!y!;!-!\"!\",!;!.!\"!\"?!;!(!\"!\"M!;!%!\"!\"S!;!1!\"!!6!<!)!\"!!F!<!&!\"!!N!<!'!\"!!Y!<!.!\"!!i!<!$!\"!!p!<!&!\"!!y!<!-!\"!\"+!<!0!\"!\"?!<!(!\"!\"M!<!%!\"!\"S!<!1!\"!!6!=!)!\"!!F!=!&!\"!!O!=!&!\"!!Y!=!'!\"!!e!=!\"!\"!!i!=!\"!\"!!p!=!&!\"!!y!=!&!\"!\"*!=!(!\"!\"5!=!'!\"!\"?!=!)!\"!\"M!=!%!\"!\"Y!=!%!\"!!5!>!%!\"!!:!>!%!\"!!F!>!&!\"!!O!>!&!\"!!X!>!'!\"!!q!>!%!\"!!y!>!&!\"!\"*!>!&!\"!\"6!>!&!\"!\"?!>!*!\"!\"M!>!%!\"!\"Y!>!%!\"!%2!>!\"!\"!%8!>!\"!\"!%<!>!\"!\"!%D!>!$!\"!%M!>!$!\"!!5!?!%!\"!!;!?!%!\"!!F!?!&!\"!!O!?!&!\"!!X!?!&!\"!!q!?!%!\"!!y!?!&!\"!\"*!?!%!\"!\"7!?!&!\"!\"?!?!*!\"!\"M!?!%!\"!\"Y!?!%!\"!%3!?!\"!\"!%8!?!\"!\"!%:!?!$!\"!%C!?!\"!\"!%F!?!\"!\"!%M!?!\"!\"!!5!@!%!\"!!;!@!%!\"!!F!@!&!\"!!N!@!&!\"!!W!@!&!\"!!p!@!&!\"!!y!@!&!\"!\")!@!&!\"!\"7!@!&!\"!\"?!@!%!\"!\"D!@!&!\"!\"M!@!%!\"!\"Y!@!%!\"!%3!@!\"!\"!"
  "%7!@!\"!\"!%<!@!\"!\"!%C!@!\"!\"!%G!@!\"!\"!%L!@!\"!\"!!4!A!%!\"!!;!A!%!\"!!F!A!-!\"!!W!A!&!\"!!p!A!&!\"!!y!A!&!\"!\")!A!&!\"!\"7!A!&!\"!\"?!A!%!\"!\"E!A!%!\"!\"M!A!%!\"!\"Y!A!%!\"!%3!A!\"!\"!%7!A!\"!\"!%<!A!\"!\"!%C!A!\"!\"!%G!A!\"!\"!%L!A!%!\"!!4!B!%!\"!!;!B!&!\"!!F!B!-!\"!!W!B!&!\"!!o!B!&!\"!!y!B!,!\"!\")!B!&!\"!\"8!B!%!\"!\"?!B!%!\"!\"F!B!%!\"!\"M!B!%!\"!\"Y!B!%!\"!%4!B!\"!\"!%7!B!\"!\"!%<!B!\"!\"!%C!B!\"!\"!%F!B!#!\"!%L!B!\"!\"!%O!B!#!\"!!4!C!%!\"!!<!C!%!\"!!F!C!.!\"!!W!C!&!\"!!m!C!'!\"!!y!C!,!\"!\")!C!&!\"!\"8!C!%!\"!\"?!C!%!\"!\"F!C!&!\"!\"M!C!%!\"!\"Y!C!%!\"!%4!C!\"!\"!%6!C!\"!\"!%<!C!\"!\"!%D!C!%!\"!%L!C!\"!\"!%P!C!\"!\"!!3!D!%!\"!!<!D!&!\"!!F!D!&!\"!!O!D!&!\"!!W!D!&!\"!!l!D!'!\"!!y!D!,!\"!\")!D!&!\"!\"7!D!&!\"!\"?!D!%!\"!\"G!D!%!\"!\"M!D!%!\"!\"Y!D!%!\"!%4!D!\"!\"!%6!D!\"!\"!%<!D!\"!\"!%G!D!\"!\"!%L!D!\"!\"!%P!D!\"!\"!!3!E!/!\"!!F!E!&!\"!!P!E!&!\"!!W!E!&!\"!!k!E!'!\"!!y!E!,!\"!\")!E!&!\"!\"7!E!&!\"!\"?!E!%!\"!\"H!E!%!\"!\"M!E!%!\"!\"Y!E!%!\"!%5!E!#!\"!%<!E!\"!\"!%@!E!\"!\"!%F!E!\"!\"!%I!E!\"!\"!%L!E!\"!\"!%O!E!#!\"!!2!F!0!\"!!F!F!&!\"!"
  "!P!F!&!\"!!W!F!'!\"!!j!F!&!\"!!y!F!&!\"!\")!F!&!\"!\"7!F!&!\"!\"?!F!%!\"!\"H!F!*!\"!\"Y!F!%!\"!%5!F!\"!\"!%<!F!\"!\"!%@!F!\"!\"!%C!F!$!\"!%I!F!\"!\"!%M!F!$!\"!!2!G!1!\"!!F!G!&!\"!!P!G!&!\"!!X!G!&!\"!!i!G!&!\"!!y!G!&!\"!\"*!G!&!\"!\"6!G!&!\"!\"?!G!%!\"!\"I!G!)!\"!\"Y!G!%!\"!!2!H!1!\"!!F!H!&!\"!!P!H!&!\"!!X!H!(!\"!!e!H!\"!\"!!i!H!%!\"!!y!H!&!\"!\"*!H!'!\"!\"5!H!'!\"!\"?!H!%!\"!\"J!H!(!\"!\"Y!H!%!\"!!1!I!&!\"!!=!I!&!\"!!F!I!&!\"!!O!I!&!\"!!Y!I!.!\"!!h!I!.!\"!!y!I!&!\"!\"+!I!0!\"!\"?!I!%!\"!\"J!I!(!\"!\"Y!I!%!\"!!1!J!&!\"!!>!J!&!\"!!F!J!/!\"!!Z!J!-!\"!!h!J!.!\"!!y!J!&!\"!\",!J!.!\"!\"?!J!%!\"!\"K!J!'!\"!\"Y!J!%!\"!!1!K!%!\"!!>!K!&!\"!!F!K!.!\"!![!K!,!\"!!h!K!.!\"!!y!K!&!\"!\"-!K!,!\"!\"?!K!%!\"!\"K!K!'!\"!\"Y!K!%!\"!!0!L!&!\"!!>!L!&!\"!!F!L!,!\"!!]!L!)!\"!!h!L!.!\"!!y!L!&!\"!\"/!L!(!\"!\"?!L!%!\"!\"L!L!&!\"!\"Y!L!%!\"!#T!Z!#!\"!\"^![!#!\"!\"z![!#!\"!#8![!\"!\"!#J![!#!\"!#U![!\"!\"!#X![!#!\"!#c![!$!\"!!0!\\!*!\"!!=!\\!&!\"!!D!\\!#!\"!!M!\\!\"!\"!!Q!\\!#!\"!!Y!\\!\"!\"!!^!\\!%!\"!!j!\\!#!\"!!q!\\!\"!\"!!y!\\!&!\"!\"'!\\!&!\"!\"H!\\!#!\"!\"P!\\!\"!\"!"
  "\"^!\\!#!\"!\"z!\\!#!\"!#8!\\!#!\"!#J!\\!#!\"!#Q!\\!#!\"!#T!\\!#!\"!#X!\\!#!\"!#b!\\!#!\"!#{!\\!\"!\"!!4!]!\"!\"!!<!]!#!\"!!A!]!#!\"!!E!]!\"!\"!!L!]!#!\"!!Q!]!#!\"!!Y!]!\"!\"!!a!]!#!\"!!j!]!\"!\"!!p!]!$!\"!!y!]!\"!\"!!}!]!#!\"!\"&!]!#!\"!\"+!]!#!\"!\"H!]!#!\"!\"P!]!\"!\"!\"z!]!#!\"!#9!]!\"!\"!#H!]!&!\"!#P!]!\"!\"!#S!]!\"!\"!#b!]!\"!\"!#{!]!\"!\"!!4!^!\"!\"!!;!^!#!\"!!E!^!#!\"!!L!^!\"!\"!!Q!^!$!\"!!Y!^!\"!\"!!b!^!\"!\"!!i!^!#!\"!!p!^!\"!\"!!r!^!\"!\"!!y!^!\"!\"!!~!^!\"!\"!\"%!^!#!\"!\"H!^!#!\"!\"P!^!\"!\"!\"z!^!#!\"!#J!^!#!\"!#b!^!\"!\"!#{!^!\"!\"!!4!_!\"!\"!!:!_!#!\"!!E!_!#!\"!!L!_!\"!\"!!Q!_!\"!\"!!S!_!#!\"!!Y!_!\"!\"!!b!_!\"!\"!!i!_!\"!\"!!o!_!#!\"!!r!_!#!\"!!y!_!\"!\"!!~!_!\"!\"!\"$!_!#!\"!\"H!_!#!\"!\"P!_!\"!\"!\"T!_!#!\"!\"W!_!$!\"!\"^!_!#!\"!\"d!_!%!\"!\"l!_!%!\"!\"v!_!$!\"!\"z!_!#!\"!#\"!_!$!\"!#-!_!#!\"!#3!_!#!\"!#7!_!%!\"!#F!_!$!\"!#J!_!#!\"!#P!_!%!\"!#X!_!#!\"!#`!_!&!\"!#h!_!%!\"!#p!_!#!\"!#s!_!$!\"!#y!_!&!\"!!4!`!\"!\"!!:!`!\"!\"!!F!`!\"!\"!!K!`!#!\"!!Q!`!\"!\"!!T!`!\"!\"!!Y!`!\"!\"!!a!`!#!\"!!i!`!\"!\"!!o!`!\"!\"!!s!`!\"!\"!!y!`!\"!\"!!}!`!#!\"!"
  "\"$!`!\"!\"!\"H!`!#!\"!\"P!`!\"!\"!\"T!`!$!\"!\"Y!`!#!\"!\"^!`!#!\"!\"c!`!#!\"!\"g!`!\"!\"!\"k!`!#!\"!\"o!`!#!\"!\"u!`!#!\"!\"y!`!$!\"!#!!`!#!\"!#%!`!\"!\"!#.!`!\"!\"!#3!`!\"!\"!#7!`!\"!\"!#:!`!#!\"!#E!`!#!\"!#I!`!$!\"!#O!`!#!\"!#S!`!#!\"!#X!`!#!\"!#b!`!\"!\"!#g!`!#!\"!#k!`!#!\"!#p!`!$!\"!#u!`!#!\"!#{!`!\"!\"!!4!a!\"!\"!!:!a!\"!\"!!F!a!#!\"!!K!a!\"!\"!!Q!a!\"!\"!!T!a!#!\"!!Y!a!\"!\"!!^!a!%!\"!!h!a!#!\"!!o!a!\"!\"!!s!a!\"!\"!!y!a!&!\"!\"$!a!\"!\"!\">!a!\"!\"!\"H!a!#!\"!\"P!a!\"!\"!\"T!a!#!\"!\"Z!a!\"!\"!\"^!a!#!\"!\"b!a!#!\"!\"j!a!#!\"!\"p!a!#!\"!\"t!a!#!\"!\"z!a!#!\"!\"~!a!#!\"!#&!a!\"!\"!#.!a!\"!\"!#3!a!\"!\"!#;!a!\"!\"!#D!a!#!\"!#J!a!#!\"!#N!a!#!\"!#T!a!#!\"!#X!a!#!\"!#b!a!\"!\"!#f!a!#!\"!#l!a!#!\"!#p!a!#!\"!#v!a!\"!\"!#{!a!\"!\"!!4!b!\"!\"!!:!b!\"!\"!!F!b!#!\"!!K!b!\"!\"!!Q!b!\"!\"!!U!b!\"!\"!!Y!b!\"!\"!!a!b!#!\"!!h!b!\"!\"!!n!b!#!\"!!s!b!#!\"!!y!b!\"!\"!!}!b!#!\"!\"$!b!\"!\"!\"5!b!,!\"!\"H!b!#!\"!\"P!b!\"!\"!\"T!b!#!\"!\"Z!b!\"!\"!\"^!b!#!\"!\"b!b!\"!\"!\"j!b!\"!\"!\"q!b!\"!\"!\"t!b!\"!\"!\"z!b!#!\"!\"~!b!\"!\"!#&!b!\"!\"!#.!b!#!\"!#2!b!#!\"!#8!b!%!\"!#D!b!\"!\"!"
  "#J!b!#!\"!#N!b!\"!\"!#U!b!\"!\"!#X!b!#!\"!#b!b!\"!\"!#f!b!\"!\"!#m!b!\"!\"!#p!b!#!\"!#v!b!\"!\"!#{!b!\"!\"!!4!c!\"!\"!!:!c!\"!\"!!G!c!\"!\"!!J!c!#!\"!!Q!c!\"!\"!!V!c!\"!\"!!Y!c!\"!\"!!b!c!#!\"!!g!c!#!\"!!n!c!(!\"!!y!c!\"!\"!!~!c!#!\"!\"$!c!\"!\"!\">!c!\"!\"!\"H!c!#!\"!\"P!c!\"!\"!\"T!c!#!\"!\"Z!c!\"!\"!\"^!c!#!\"!\"b!c!\"!\"!\"j!c!\"!\"!\"q!c!\"!\"!\"t!c!\"!\"!\"z!c!#!\"!\"~!c!(!\"!#/!c!\"!\"!#2!c!\"!\"!#6!c!$!\"!#;!c!\"!\"!#D!c!\"!\"!#J!c!#!\"!#N!c!\"!\"!#U!c!\"!\"!#X!c!#!\"!#b!c!\"!\"!#f!c!\"!\"!#m!c!\"!\"!#p!c!#!\"!#v!c!\"!\"!#{!c!\"!\"!!4!d!\"!\"!!:!d!#!\"!!G!d!#!\"!!J!d!\"!\"!!Q!d!\"!\"!!V!d!#!\"!!Y!d!\"!\"!!b!d!#!\"!!g!d!\"!\"!!n!d!\"!\"!!t!d!\"!\"!!y!d!\"!\"!!~!d!#!\"!\"$!d!#!\"!\"I!d!\"!\"!\"P!d!\"!\"!\"T!d!#!\"!\"Z!d!\"!\"!\"^!d!#!\"!\"b!d!\"!\"!\"j!d!\"!\"!\"q!d!\"!\"!\"t!d!\"!\"!\"z!d!#!\"!\"~!d!\"!\"!#/!d!\"!\"!#2!d!\"!\"!#6!d!\"!\"!#;!d!\"!\"!#D!d!\"!\"!#J!d!#!\"!#N!d!\"!\"!#U!d!\"!\"!#X!d!#!\"!#b!d!\"!\"!#f!d!\"!\"!#m!d!\"!\"!#p!d!#!\"!#v!d!\"!\"!#{!d!\"!\"!!4!e!\"!\"!!;!e!\"!\"!!H!e!\"!\"!!J!e!\"!\"!!Q!e!\"!\"!!W!e!$!\"!!b!e!#!\"!!g!e!\"!\"!!m!e!#!\"!!t!e!#!\"!"
  "!y!e!\"!\"!!~!e!#!\"!\"%!e!\"!\"!\"I!e!\"!\"!\"O!e!#!\"!\"T!e!#!\"!\"Z!e!\"!\"!\"^!e!#!\"!\"b!e!#!\"!\"j!e!#!\"!\"p!e!#!\"!\"t!e!#!\"!\"z!e!#!\"!\"~!e!#!\"!#/!e!%!\"!#6!e!\"!\"!#;!e!\"!\"!#D!e!#!\"!#J!e!#!\"!#N!e!#!\"!#T!e!#!\"!#X!e!#!\"!#b!e!\"!\"!#f!e!#!\"!#l!e!#!\"!#p!e!#!\"!#v!e!\"!\"!#{!e!\"!\"!!4!f!\"!\"!!;!f!$!\"!!A!f!#!\"!!H!f!$!\"!!Q!f!\"!\"!!W!f!$!\"!!]!f!\"!\"!!a!f!#!\"!!f!f!\"!\"!!m!f!\"!\"!!u!f!\"!\"!!y!f!\"!\"!!}!f!#!\"!\"%!f!$!\"!\"+!f!#!\"!\"I!f!$!\"!\"N!f!#!\"!\"T!f!#!\"!\"Z!f!\"!\"!\"^!f!#!\"!\"c!f!#!\"!\"g!f!\"!\"!\"k!f!#!\"!\"o!f!#!\"!\"u!f!#!\"!\"y!f!$!\"!#!!f!#!\"!#&!f!\"!\"!#0!f!#!\"!#6!f!#!\"!#:!f!#!\"!#E!f!#!\"!#I!f!$!\"!#O!f!#!\"!#S!f!#!\"!#X!f!#!\"!#b!f!\"!\"!#g!f!#!\"!#k!f!#!\"!#p!f!#!\"!#v!f!\"!\"!#{!f!#!\"!!4!g!\"!\"!!=!g!&!\"!!H!g!#!\"!!Q!g!\"!\"!!X!g!#!\"!!]!g!&!\"!!f!g!\"!\"!!l!g!#!\"!!u!g!#!\"!!y!g!&!\"!\"'!g!&!\"!\"J!g!&!\"!\"T!g!#!\"!\"Z!g!\"!\"!\"^!g!#!\"!\"d!g!%!\"!\"l!g!%!\"!\"v!g!$!\"!\"z!g!#!\"!#\"!g!%!\"!#0!g!#!\"!#7!g!$!\"!#;!g!\"!\"!#F!g!$!\"!#J!g!#!\"!#P!g!%!\"!#X!g!#!\"!#b!g!\"!\"!#h!g!%!\"!#p!g!#!\"!"
  "#v!g!\"!\"!#|!g!#!\"!!e!h!#!\"!!e!i!\"!\"!!R\",!$!\"!!z\",!$!\"!\"0\",!#!\"!\"7\",!$!\"!$\"\",!#!\"!$0\",!$!\"!!=\"-!(!\"!!R\"-!$!\"!!z\"-!$!\"!\"/\"-!$!\"!\"5\"-!&!\"!\"=\"-!#!\"!#n\"-!%!\"!$!\"-!$!\"!$0\"-!$!\"!!=\".!*!\"!!R\".!$!\"!!z\".!$!\"!\"0\".!#!\"!\"5\".!$!\"!\"<\".!$!\"!#m\".!&!\"!$\"\".!#!\"!$0\".!$!\"!!=\"/!$!\"!!B\"/!%!\"!!R\"/!$!\"!!z\"/!$!\"!\"5\"/!$!\"!\"<\"/!$!\"!#m\"/!&!\"!$0\"/!$!\"!!=\"0!$!\"!!C\"0!$!\"!!I\"0!&!\"!!R\"0!$!\"!!V\"0!%!\"!!]\"0!$!\"!!a\"0!%!\"!!j\"0!&!\"!!t\"0!%!\"!!z\"0!$!\"!!~\"0!%!\"!\"'\"0!$!\"!\"+\"0!#!\"!\"/\"0!$!\"!\"3\"0!/!\"!#m\"0!#!\"!#p\"0!$!\"!#w\"0!$!\"!#{\"0!#!\"!$!\"0!$!\"!$'\"0!&!\"!$0\"0!$!\"!!=\"1!$!\"!!B\"1!$!\"!!H\"1!(!\"!!R\"1!*!\"!!]\"1!*!\"!!i\"1!'!\"!!r\"1!'!\"!!z\"1!*!\"!\"'\"1!'!\"!\"/\"1!$!\"!\"3\"1!/!\"!#l\"1!$!\"!#p\"1!$!\"!#w\"1!'!\"!$!\"1!$!\"!$&\"1!(!\"!$0\"1!$!\"!!=\"2!(!\"!!I\"2!\"!\"!!M\"2!$!\"!!R\"2!%!\"!!X\"2!$!\"!!]\"2!%!\"!!c\"2!$!\"!!i\"2!$!\"!!q\"2!%!\"!!z\"2!%!\"!\"\"\"2!$!\"!\"'\"2!'!\"!\"/\"2!$!\"!\"5\"2!$!\"!\"<\"2!$!\"!#l\"2!$!\"!#q\"2!#!\"!#w\"2!'!\"!$!\"2!$!\"!$'\"2!\"!\"!$+\"2!$!\"!$0\"2!$!\"!!=\"3!)!\"!!M\"3!$!\"!!R\"3!$!\"!"
  "!X\"3!$!\"!!]\"3!$!\"!!c\"3!$!\"!!i\"3!$!\"!!q\"3!$!\"!!z\"3!$!\"!\"\"\"3!$!\"!\"'\"3!%!\"!\"/\"3!$!\"!\"5\"3!$!\"!\"<\"3!$!\"!#l\"3!#!\"!#q\"3!$!\"!#w\"3!%!\"!$!\"3!$!\"!$+\"3!$!\"!$0\"3!$!\"!!=\"4!$!\"!!C\"4!$!\"!!I\"4!(!\"!!R\"4!$!\"!!X\"4!$!\"!!]\"4!$!\"!!c\"4!$!\"!!i\"4!&!\"!!q\"4!$!\"!!z\"4!$!\"!\"\"\"4!$!\"!\"'\"4!$!\"!\"/\"4!$!\"!\"5\"4!$!\"!\"<\"4!$!\"!#k\"4!$!\"!#q\"4!$!\"!#w\"4!$!\"!$!\"4!$!\"!$'\"4!(!\"!$0\"4!$!\"!!=\"5!$!\"!!C\"5!$!\"!!H\"5!)!\"!!R\"5!$!\"!!X\"5!$!\"!!]\"5!$!\"!!c\"5!$!\"!!j\"5!&!\"!!q\"5!$!\"!!z\"5!$!\"!\"\"\"5!$!\"!\"'\"5!$!\"!\"/\"5!$!\"!\"5\"5!$!\"!\"<\"5!$!\"!#k\"5!+!\"!#w\"5!$!\"!$!\"5!$!\"!$&\"5!)!\"!$0\"5!$!\"!!=\"6!$!\"!!C\"6!$!\"!!H\"6!#!\"!!M\"6!$!\"!!R\"6!$!\"!!X\"6!$!\"!!]\"6!$!\"!!c\"6!$!\"!!m\"6!$!\"!!q\"6!$!\"!!z\"6!$!\"!\"\"\"6!$!\"!\"'\"6!$!\"!\"/\"6!$!\"!\"5\"6!$!\"!\"<\"6!$!\"!#j\"6!,!\"!#w\"6!$!\"!$!\"6!$!\"!$&\"6!#!\"!$+\"6!$!\"!$0\"6!$!\"!!=\"7!$!\"!!C\"7!$!\"!!H\"7!#!\"!!M\"7!$!\"!!R\"7!$!\"!!X\"7!$!\"!!]\"7!$!\"!!c\"7!$!\"!!i\"7!\"!\"!!m\"7!$!\"!!q\"7!%!\"!!z\"7!$!\"!\"\"\"7!$!\"!\"'\"7!$!\"!\"/\"7!$!\"!\"5\"7!$!\"!\"<\"7!$!\"!#j\"7!$!\"!#r\"7!$!\"!#w\"7!$!\"!"
  "$!\"7!$!\"!$&\"7!#!\"!$+\"7!$!\"!$0\"7!$!\"!!=\"8!*!\"!!H\"8!)!\"!!R\"8!$!\"!!X\"8!$!\"!!]\"8!$!\"!!c\"8!$!\"!!i\"8!'!\"!!r\"8!'!\"!!z\"8!$!\"!\"\"\"8!$!\"!\"'\"8!$!\"!\"/\"8!$!\"!\"5\"8!$!\"!\"<\"8!&!\"!#j\"8!$!\"!#r\"8!%!\"!#w\"8!$!\"!$!\"8!$!\"!$&\"8!)!\"!$0\"8!$!\"!!=\"9!(!\"!!I\"9!$!\"!!M\"9!$!\"!!R\"9!$!\"!!X\"9!$!\"!!]\"9!$!\"!!c\"9!$!\"!!i\"9!&!\"!!s\"9!&!\"!!z\"9!$!\"!\"\"\"9!$!\"!\"'\"9!$!\"!\"/\"9!$!\"!\"5\"9!$!\"!\"=\"9!%!\"!#i\"9!$!\"!#s\"9!$!\"!#w\"9!$!\"!$!\"9!$!\"!$'\"9!$!\"!$+\"9!$!\"!$0\"9!$!\"!!>\"I!%!\"!!F\"I!(!\"!!S\"I!%!\"!!\\\"I!%!\"!!d\"I!(!\"!!p\"I!%!\"!!x\"I!#!\"!!~\"I!$!\"!\"$\"I!$!\"!\"+\"I!#!\"!\":\"I!&!\"!\"C\"I!(!\"!\"M\"I!#!\"!#l\"I!%!\"!#t\"I!(!\"!$#\"I!%!\"!$,\"I!%!\"!$6\"I!%!\"!$>\"I!'!\"!$H\"I!)!\"!$T\"I!%!\"!$]\"I!#!\"!$r\"I!&!\"!${\"I!(!\"!%'\"I!#!\"!!>\"J!%!\"!!F\"J!)!\"!!Q\"J!(!\"!!Z\"J!(!\"!!d\"J!)!\"!!p\"J!%!\"!!x\"J!#!\"!!~\"J!$!\"!\"$\"J!$!\"!\"+\"J!#!\"!\"9\"J!(!\"!\"C\"J!(!\"!\"M\"J!#!\"!#l\"J!%!\"!#t\"J!)!\"!$!\"J!(!\"!$*\"J!(!\"!$6\"J!%!\"!$>\"J!)!\"!$H\"J!)!\"!$T\"J!%!\"!$]\"J!#!\"!$q\"J!(!\"!${\"J!(!\"!%'\"J!#!\"!!>\"K!&!\"!!F\"K!#!\"!!K\"K!$!\"!!Q\"K!$!\"!"
  "!W\"K!\"!\"!![\"K!\"!\"!!_\"K!$!\"!!d\"K!#!\"!!i\"K!$!\"!!p\"K!&!\"!!x\"K!#!\"!!~\"K!$!\"!\"$\"K!%!\"!\"+\"K!#!\"!\"8\"K!$!\"!\"C\"K!#!\"!\"M\"K!#!\"!#l\"K!&!\"!#t\"K!#!\"!#y\"K!$!\"!$!\"K!$!\"!$'\"K!\"!\"!$+\"K!\"!\"!$/\"K!$!\"!$6\"K!&!\"!$>\"K!#!\"!$C\"K!$!\"!$K\"K!#!\"!$T\"K!&!\"!$]\"K!#!\"!$p\"K!$!\"!${\"K!#!\"!%'\"K!#!\"!!>\"L!#!\"!!A\"L!#!\"!!F\"L!#!\"!!L\"L!#!\"!!P\"L!$!\"!!`\"L!#!\"!!d\"L!#!\"!!j\"L!#!\"!!p\"L!#!\"!!s\"L!#!\"!!x\"L!#!\"!!~\"L!$!\"!\"$\"L!%!\"!\"+\"L!#!\"!\"8\"L!$!\"!\"C\"L!#!\"!\"M\"L!#!\"!#l\"L!#!\"!#o\"L!#!\"!#t\"L!#!\"!#z\"L!#!\"!#~\"L!$!\"!$0\"L!#!\"!$6\"L!#!\"!$9\"L!#!\"!$>\"L!#!\"!$D\"L!#!\"!$K\"L!#!\"!$T\"L!#!\"!$W\"L!#!\"!$]\"L!#!\"!$p\"L!$!\"!${\"L!#!\"!%'\"L!#!\"!!=\"M!#!\"!!A\"M!#!\"!!F\"M!#!\"!!K\"M!$!\"!!P\"M!#!\"!!_\"M!$!\"!!d\"M!#!\"!!i\"M!$!\"!!o\"M!#!\"!!s\"M!#!\"!!x\"M!#!\"!!~\"M!$!\"!\"$\"M!&!\"!\"+\"M!#!\"!\"8\"M!%!\"!\"C\"M!#!\"!\"M\"M!#!\"!#k\"M!#!\"!#o\"M!#!\"!#t\"M!#!\"!#y\"M!$!\"!#~\"M!#!\"!$/\"M!$!\"!$5\"M!#!\"!$9\"M!#!\"!$>\"M!#!\"!$C\"M!$!\"!$K\"M!#!\"!$S\"M!#!\"!$W\"M!#!\"!$]\"M!#!\"!$p\"M!%!\"!${\"M!#!\"!%'\"M!#!\"!!=\"N!#!\"!!A\"N!#!\"!!F\"N!(!\"!"
  "!P\"N!#!\"!!_\"N!#!\"!!d\"N!(!\"!!o\"N!#!\"!!s\"N!#!\"!!x\"N!*!\"!\"$\"N!#!\"!\"'\"N!#!\"!\"+\"N!#!\"!\"9\"N!&!\"!\"C\"N!(!\"!\"M\"N!#!\"!#k\"N!#!\"!#o\"N!#!\"!#t\"N!(!\"!#~\"N!#!\"!$/\"N!#!\"!$5\"N!#!\"!$9\"N!#!\"!$>\"N!(!\"!$K\"N!#!\"!$S\"N!#!\"!$W\"N!#!\"!$]\"N!#!\"!$q\"N!&!\"!${\"N!(!\"!%'\"N!#!\"!!=\"O!#!\"!!A\"O!$!\"!!F\"O!)!\"!!P\"O!#!\"!!^\"O!$!\"!!d\"O!)!\"!!o\"O!#!\"!!s\"O!$!\"!!x\"O!*!\"!\"$\"O!#!\"!\"'\"O!$!\"!\"+\"O!#!\"!\";\"O!&!\"!\"C\"O!(!\"!\"M\"O!#!\"!#k\"O!#!\"!#o\"O!$!\"!#t\"O!)!\"!#~\"O!#!\"!$.\"O!$!\"!$5\"O!#!\"!$9\"O!$!\"!$>\"O!'!\"!$K\"O!#!\"!$S\"O!#!\"!$W\"O!$!\"!$]\"O!#!\"!$s\"O!&!\"!${\"O!(!\"!%'\"O!#!\"!!<\"P!$!\"!!B\"P!#!\"!!F\"P!#!\"!!L\"P!$!\"!!P\"P!#!\"!!]\"P!$!\"!!d\"P!#!\"!!j\"P!$!\"!!n\"P!$!\"!!t\"P!#!\"!!x\"P!#!\"!!~\"P!$!\"!\"$\"P!#!\"!\"(\"P!&!\"!\"=\"P!$!\"!\"C\"P!#!\"!\"M\"P!#!\"!#j\"P!$!\"!#p\"P!#!\"!#t\"P!#!\"!#z\"P!$!\"!#~\"P!#!\"!$-\"P!$!\"!$4\"P!$!\"!$:\"P!#!\"!$>\"P!#!\"!$B\"P!$!\"!$K\"P!#!\"!$R\"P!$!\"!$X\"P!#!\"!$]\"P!#!\"!$u\"P!$!\"!${\"P!#!\"!%'\"P!#!\"!!<\"Q!)!\"!!F\"Q!#!\"!!L\"Q!$!\"!!P\"Q!$!\"!!\\\"Q!$!\"!!d\"Q!#!\"!!j\"Q!$!\"!!n\"Q!)!\"!!x\"Q!#!\"!"
  "!~\"Q!$!\"!\"$\"Q!#!\"!\"(\"Q!&!\"!\">\"Q!#!\"!\"C\"Q!#!\"!\"M\"Q!#!\"!#j\"Q!)!\"!#t\"Q!#!\"!#z\"Q!$!\"!#~\"Q!$!\"!$,\"Q!$!\"!$4\"Q!)!\"!$>\"Q!#!\"!$C\"Q!#!\"!$K\"Q!#!\"!$R\"Q!)!\"!$]\"Q!#!\"!$v\"Q!#!\"!${\"Q!#!\"!%'\"Q!#!\"!!<\"R!*!\"!!F\"R!#!\"!!L\"R!$!\"!!P\"R!%!\"!!W\"R!\"!\"!![\"R!$!\"!!d\"R!#!\"!!j\"R!$!\"!!n\"R!*!\"!!x\"R!#!\"!!~\"R!$!\"!\"$\"R!#!\"!\")\"R!%!\"!\"8\"R!\"!\"!\"=\"R!$!\"!\"C\"R!#!\"!\"M\"R!#!\"!#j\"R!*!\"!#t\"R!#!\"!#z\"R!$!\"!#~\"R!%!\"!$'\"R!\"!\"!$+\"R!$!\"!$4\"R!*!\"!$>\"R!#!\"!$C\"R!$!\"!$K\"R!#!\"!$R\"R!*!\"!$]\"R!#!\"!$p\"R!\"!\"!$u\"R!$!\"!${\"R!#!\"!%'\"R!#!\"!!;\"S!$!\"!!B\"S!$!\"!!F\"S!)!\"!!Q\"S!(!\"!!Z\"S!)!\"!!d\"S!)!\"!!m\"S!$!\"!!t\"S!$!\"!!x\"S!#!\"!!~\"S!$!\"!\"$\"S!#!\"!\")\"S!%!\"!\"8\"S!)!\"!\"C\"S!(!\"!\"M\"S!(!\"!#i\"S!$!\"!#p\"S!$!\"!#t\"S!)!\"!$!\"S!(!\"!$*\"S!)!\"!$3\"S!$!\"!$:\"S!$!\"!$>\"S!#!\"!$D\"S!#!\"!$H\"S!)!\"!$Q\"S!$!\"!$X\"S!$!\"!$]\"S!(!\"!$p\"S!)!\"!${\"S!(!\"!%'\"S!(!\"!!;\"T!#!\"!!C\"T!#!\"!!F\"T!(!\"!!S\"T!%!\"!!Z\"T!)!\"!!d\"T!(!\"!!m\"T!#!\"!!u\"T!#!\"!!x\"T!#!\"!!~\"T!$!\"!\"$\"T!#!\"!\"*\"T!$!\"!\"9\"T!&!\"!\"C\"T!(!\"!\"M\"T!(!\"!#i\"T!#!\"!"
  "#q\"T!#!\"!#t\"T!(!\"!$#\"T!%!\"!$*\"T!)!\"!$3\"T!#!\"!$;\"T!#!\"!$>\"T!#!\"!$D\"T!$!\"!$H\"T!)!\"!$Q\"T!#!\"!$Y\"T!#!\"!$]\"T!(!\"!$q\"T!&!\"!${\"T!(!\"!%'\"T!(!\"!\"-\"W!+!\"!$e\"W!+!\"!\"-\"X!+!\"!$e\"X!+!\"!!A\"z!#!\"!!f\"z!#!\"!!y\"z!$!\"!\"=\"z!#!\"!\"m\"z!#!\"!\"u\"z!#!\"!#6\"z!#!\"!#B\"z!#!\"!#h\"z!\"!\"!#r\"z!#!\"!$6\"z!#!\"!$=\"z!\"!\"!$M\"z!#!\"!!2\"{!&!\"!!=\"{!#!\"!!A\"{!\"!\"!!f\"{!\"!\"!!m\"{!\"!\"!!x\"{!#!\"!\"3\"{!\"!\"!\"=\"{!#!\"!\"R\"{!\"!\"!\"V\"{!\"!\"!\"m\"{!#!\"!\"u\"{!\"!\"!#6\"{!#!\"!#C\"{!#!\"!#i\"{!\"!\"!#k\"{!#!\"!#r\"{!#!\"!$)\"{!#!\"!$-\"{!\"!\"!$6\"{!#!\"!$<\"{!\"!\"!$>\"{!\"!\"!$M\"{!#!\"!!2\"|!\"!\"!!6\"|!#!\"!!=\"|!\"!\"!!?\"|!\"!\"!!e\"|!\"!\"!!m\"|!\"!\"!!x\"|!\"!\"!\"3\"|!\"!\"!\"=\"|!#!\"!\"S\"|!$!\"!\"k\"|!&!\"!\"t\"|!\"!\"!#6\"|!#!\"!#C\"|!\"!\"!#j\"|!\"!\"!#m\"|!\"!\"!$(\"|!\"!\"!$+\"|!#!\"!$6\"|!#!\"!$<\"|!\"!\"!$?\"|!\"!\"!$M\"|!#!\"!!2\"}!\"!\"!!7\"}!\"!\"!!m\"}!\"!\"!!x\"}!\"!\"!\"3\"}!\"!\"!\"=\"}!#!\"!\"m\"}!#!\"!#6\"}!#!\"!$6\"}!#!\"!$M\"}!#!\"!!2\"~!\"!\"!!7\"~!\"!\"!!<\"~!%!\"!!D\"~!#!\"!!G\"~!$!\"!!L\"~!$!\"!!X\"~!#!\"!![\"~!$!\"!!b\"~!#!\"!!h\"~!\"!\"!!k\"~!&!\"!!v\"~!&!\"!!~\"~!%!\"!"
  "\"(\"~!#!\"!\"+\"~!$!\"!\"1\"~!&!\"!\"=\"~!#!\"!\"@\"~!$!\"!\"I\"~!%!\"!\"R\"~!%!\"!\"\\\"~!%!\"!\"i\"~!$!\"!\"m\"~!#!\"!\"s\"~!%!\"!\"{\"~!#!\"!\"~\"~!$!\"!#)\"~!$!\"!#-\"~!#!\"!#6\"~!#!\"!#9\"~!$!\"!#A\"~!%!\"!#I\"~!#!\"!#L\"~!$!\"!#U\"~!$!\"!#Y\"~!#!\"!#b\"~!#!\"!#e\"~!#!\"!#j\"~!%!\"!#r\"~!#!\"!#}\"~!$!\"!$#\"~!#!\"!$)\"~!%!\"!$6\"~!#!\"!$<\"~!$!\"!$C\"~!#!\"!$F\"~!$!\"!$M\"~!#!\"!$P\"~!$!\"!!2#!!\"!\"!!6#!!#!\"!!<#!!\"!\"!!?#!!#!\"!!D#!!$!\"!!I#!!$!\"!!N#!!#!\"!!X#!!$!\"!!]#!!#!\"!!b#!!#!\"!!h#!!\"!\"!!m#!!\"!\"!!x#!!\"!\"!!}#!!#!\"!\"##!!#!\"!\"(#!!$!\"!\"-#!!#!\"!\"3#!!\"!\"!\"=#!!$!\"!\"B#!!#!\"!\"H#!!#!\"!\"L#!!#!\"!\"R#!!\"!\"!\"U#!!#!\"!\"[#!!#!\"!\"_#!!\"!\"!\"h#!!#!\"!\"l#!!$!\"!\"r#!!#!\"!\"v#!!#!\"!\"{#!!$!\"!#\"#!!#!\"!#(#!!#!\"!#,#!!$!\"!#6#!!$!\"!#<#!!\"!\"!#A#!!\"!\"!#D#!!#!\"!#I#!!$!\"!#N#!!#!\"!#T#!!#!\"!#X#!!$!\"!#b#!!$!\"!#i#!!#!\"!#m#!!#!\"!#r#!!#!\"!#|#!!#!\"!$\"#!!$!\"!$(#!!#!\"!$,#!!#!\"!$6#!!#!\"!$;#!!#!\"!$?#!!\"!\"!$C#!!$!\"!$H#!!#!\"!$M#!!$!\"!$R#!!#!\"!!2#\"!&!\"!!@#\"!\"!\"!!D#\"!#!\"!!J#\"!\"!\"!!O#\"!\"!\"!!X#\"!#!\"!!^#\"!\"!\"!!b#\"!#!\"!"
  "!h#\"!\"!\"!!m#\"!\"!\"!!x#\"!\"!\"!!|#\"!#!\"!\"$#\"!#!\"!\"(#\"!#!\"!\".#\"!\"!\"!\"3#\"!\"!\"!\"=#\"!#!\"!\"C#\"!\"!\"!\"G#\"!#!\"!\"M#\"!#!\"!\"V#\"!\"!\"!\"Z#\"!#!\"!\"g#\"!#!\"!\"m#\"!#!\"!\"q#\"!#!\"!\"w#\"!#!\"!\"{#\"!#!\"!###\"!\"!\"!#'#\"!#!\"!#-#\"!#!\"!#6#\"!#!\"!#<#\"!#!\"!#E#\"!\"!\"!#I#\"!#!\"!#O#\"!\"!\"!#S#\"!#!\"!#Y#\"!#!\"!#b#\"!#!\"!#h#\"!#!\"!#n#\"!#!\"!#r#\"!#!\"!#{#\"!#!\"!$##\"!#!\"!$'#\"!#!\"!$-#\"!#!\"!$6#\"!#!\"!$:#\"!#!\"!$@#\"!\"!\"!$C#\"!#!\"!$I#\"!\"!\"!$M#\"!#!\"!$S#\"!\"!\"!!2##!\"!\"!!6##!#!\"!!=##!%!\"!!D##!#!\"!!J##!\"!\"!!O##!\"!\"!!X##!#!\"!!^##!\"!\"!!b##!#!\"!!h##!\"!\"!!m##!\"!\"!!x##!\"!\"!!|##!\"!\"!\"%##!\"!\"!\"(##!#!\"!\".##!\"!\"!\"3##!\"!\"!\"=##!#!\"!\"C##!\"!\"!\"G##!\"!\"!\"N##!\"!\"!\"S##!%!\"!\"Z##!\"!\"!\"g##!\"!\"!\"m##!#!\"!\"q##!\"!\"!\"x##!\"!\"!\"{##!#!\"!####!\"!\"!#'##!\"!\"!#-##!#!\"!#6##!#!\"!#=##!\"!\"!#B##!%!\"!#I##!#!\"!#O##!\"!\"!#S##!\"!\"!#Y##!#!\"!#b##!#!\"!#h##!\"!\"!#o##!\"!\"!#r##!#!\"!#{##!\"!\"!$###!#!\"!$'##!\"!\"!$.##!\"!\"!$6##!#!\"!$:##!\"!\"!$@##!\"!\"!$C##!#!\"!$I##!\"!\"!$M##!#!\"!$S##!\"!\"!!2#$!\"!\"!!7#$!#!\"!!;#$!$!\"!"
  "!@#$!\"!\"!!D#$!#!\"!!J#$!\"!\"!!O#$!\"!\"!!X#$!#!\"!!^#$!\"!\"!!b#$!#!\"!!h#$!\"!\"!!m#$!\"!\"!!x#$!\"!\"!!|#$!\"!\"!\"%#$!\"!\"!\"(#$!#!\"!\".#$!\"!\"!\"3#$!\"!\"!\"=#$!#!\"!\"C#$!\"!\"!\"G#$!\"!\"!\"N#$!\"!\"!\"Q#$!$!\"!\"V#$!\"!\"!\"Z#$!\"!\"!\"g#$!\"!\"!\"m#$!#!\"!\"q#$!\"!\"!\"x#$!\"!\"!\"{#$!#!\"!###$!\"!\"!#'#$!\"!\"!#-#$!#!\"!#6#$!#!\"!#=#$!\"!\"!#@#$!$!\"!#E#$!\"!\"!#I#$!#!\"!#O#$!\"!\"!#S#$!\"!\"!#Y#$!#!\"!#b#$!#!\"!#h#$!\"!\"!#o#$!\"!\"!#r#$!#!\"!#{#$!\"!\"!$##$!#!\"!$'#$!\"!\"!$.#$!\"!\"!$6#$!#!\"!$:#$!(!\"!$C#$!#!\"!$I#$!\"!\"!$M#$!#!\"!$S#$!\"!\"!!2#%!\"!\"!!7#%!#!\"!!;#%!\"!\"!!@#%!\"!\"!!D#%!#!\"!!J#%!\"!\"!!O#%!\"!\"!!X#%!#!\"!!^#%!\"!\"!!b#%!#!\"!!h#%!\"!\"!!m#%!\"!\"!!x#%!\"!\"!!|#%!\"!\"!\"%#%!\"!\"!\"(#%!#!\"!\".#%!\"!\"!\"3#%!\"!\"!\"=#%!#!\"!\"C#%!\"!\"!\"G#%!\"!\"!\"N#%!\"!\"!\"Q#%!\"!\"!\"V#%!\"!\"!\"Z#%!\"!\"!\"g#%!\"!\"!\"m#%!#!\"!\"q#%!\"!\"!\"x#%!\"!\"!\"{#%!#!\"!###%!\"!\"!#'#%!\"!\"!#-#%!#!\"!#6#%!#!\"!#=#%!\"!\"!#@#%!\"!\"!#E#%!\"!\"!#I#%!#!\"!#O#%!\"!\"!#S#%!\"!\"!#Y#%!#!\"!#b#%!#!\"!#h#%!\"!\"!#o#%!\"!\"!#r#%!#!\"!#{#%!\"!\"!$##%!#!\"!$'#%!\"!\"!"
  "$.#%!\"!\"!$6#%!#!\"!$:#%!\"!\"!$C#%!#!\"!$I#%!\"!\"!$M#%!#!\"!$S#%!\"!\"!!2#&!\"!\"!!7#&!#!\"!!;#&!\"!\"!!@#&!\"!\"!!D#&!#!\"!!J#&!\"!\"!!O#&!\"!\"!!X#&!#!\"!!^#&!\"!\"!!b#&!#!\"!!h#&!\"!\"!!m#&!\"!\"!!x#&!\"!\"!!|#&!#!\"!\"$#&!#!\"!\"(#&!#!\"!\".#&!\"!\"!\"3#&!\"!\"!\"=#&!#!\"!\"C#&!\"!\"!\"G#&!#!\"!\"M#&!#!\"!\"Q#&!\"!\"!\"V#&!\"!\"!\"Z#&!#!\"!\"g#&!#!\"!\"m#&!#!\"!\"q#&!#!\"!\"w#&!#!\"!\"{#&!#!\"!###&!\"!\"!#'#&!#!\"!#-#&!#!\"!#6#&!#!\"!#<#&!#!\"!#@#&!\"!\"!#E#&!\"!\"!#I#&!#!\"!#O#&!\"!\"!#S#&!#!\"!#Y#&!#!\"!#b#&!#!\"!#h#&!#!\"!#n#&!#!\"!#r#&!#!\"!#{#&!#!\"!$##&!#!\"!$'#&!#!\"!$-#&!#!\"!$6#&!#!\"!$:#&!#!\"!$C#&!#!\"!$I#&!\"!\"!$M#&!#!\"!$S#&!\"!\"!!2#'!\"!\"!!6#'!#!\"!!;#'!#!\"!!?#'!#!\"!!D#'!#!\"!!J#'!\"!\"!!O#'!\"!\"!!X#'!#!\"!!^#'!\"!\"!!c#'!#!\"!!g#'!#!\"!!m#'!#!\"!!x#'!\"!\"!!}#'!#!\"!\"##'!#!\"!\"(#'!#!\"!\".#'!\"!\"!\"3#'!#!\"!\"=#'!#!\"!\"C#'!\"!\"!\"H#'!#!\"!\"L#'!#!\"!\"Q#'!#!\"!\"U#'!#!\"!\"[#'!#!\"!\"_#'!\"!\"!\"h#'!#!\"!\"l#'!$!\"!\"r#'!#!\"!\"v#'!#!\"!\"{#'!#!\"!###'!\"!\"!#(#'!#!\"!#,#'!$!\"!#6#'!$!\"!#;#'!#!\"!#@#'!#!\"!#D#'!#!\"!"
  "#I#'!#!\"!#O#'!\"!\"!#T#'!#!\"!#X#'!$!\"!#b#'!#!\"!#i#'!#!\"!#m#'!#!\"!#r#'!#!\"!#|#'!#!\"!$\"#'!$!\"!$(#'!#!\"!$,#'!#!\"!$6#'!#!\"!$;#'!#!\"!$@#'!\"!\"!$C#'!#!\"!$I#'!\"!\"!$M#'!#!\"!$S#'!\"!\"!$W#'!#!\"!!2#(!&!\"!!<#(!$!\"!!@#(!\"!\"!!D#(!#!\"!!J#(!\"!\"!!O#(!\"!\"!!X#(!#!\"!!^#(!\"!\"!!d#(!$!\"!!h#(!\"!\"!!n#(!#!\"!!x#(!\"!\"!!~#(!%!\"!\"(#(!#!\"!\".#(!\"!\"!\"4#(!#!\"!\"=#(!#!\"!\"C#(!\"!\"!\"I#(!%!\"!\"R#(!$!\"!\"V#(!\"!\"!\"\\#(!%!\"!\"i#(!$!\"!\"m#(!#!\"!\"s#(!%!\"!\"{#(!#!\"!###(!\"!\"!#)#(!$!\"!#-#(!#!\"!#6#(!#!\"!#9#(!$!\"!#A#(!$!\"!#E#(!\"!\"!#I#(!#!\"!#O#(!\"!\"!#U#(!$!\"!#Y#(!#!\"!#b#(!#!\"!#j#(!%!\"!#r#(!#!\"!#}#(!$!\"!$##(!#!\"!$)#(!%!\"!$6#(!#!\"!$<#(!%!\"!$C#(!#!\"!$I#(!\"!\"!$M#(!#!\"!$S#(!\"!\"!$W#(!#!\"!#-#)!#!\"!#Y#)!#!\"!$##)!#!\"!\"T#*!\"!\"!#-#*!\"!\"!#Y#*!\"!\"!$##*!\"!\"!$=#*!\"!\"!\"T#+!\"!\"!#'#+!#!\"!#,#+!#!\"!#S#+!#!\"!#X#+!#!\"!#{#+!#!\"!$\"#+!#!\"!$=#+!\"!\"!#(#,!&!\"!#T#,!&!\"!#|#,!&!\"!!t#6!#!\"!\"A#6!#!\"!!5#7!\"!\"!!G#7!\"!\"!!t#7!#!\"!\"A#7!#!\"!\"M#7!#!\"!\"P#7!\"!\"!\"l#7!'!\"!#!#7!\"!\"!!4#8!$!\"!"
  "!G#8!\"!\"!!t#8!#!\"!\"A#8!#!\"!\"L#8!\"!\"!\"O#8!#!\"!\"S#8!\"!\"!\"l#8!\"!\"!#!#8!\"!\"!!4#9!\"!\"!!6#9!\"!\"!!G#9!\"!\"!!t#9!#!\"!\"A#9!#!\"!\"S#9!\"!\"!\"l#9!\"!\"!#!#9!\"!\"!!3#:!#!\"!!6#:!#!\"!!<#:!#!\"!!B#:!\"!\"!!E#:!&!\"!!N#:!%!\"!!\\#:!\"!\"!!n#:!%!\"!!t#:!#!\"!!w#:!$!\"!\"\"#:!%!\"!\"*#:!#!\"!\"-#:!$!\"!\";#:!%!\"!\"A#:!#!\"!\"D#:!$!\"!\"K#:!#!\"!\"Q#:!#!\"!\"[#:!\"!\"!\"l#:!\"!\"!\"t#:!#!\"!\"w#:!$!\"!\"}#:!&!\"!#(#:!$!\"!#/#:!#!\"!#2#:!#!\"!!3#;!\"!\"!!7#;!\"!\"!!<#;!#!\"!!B#;!\"!\"!!G#;!\"!\"!!M#;!#!\"!!Q#;!#!\"!!]#;!#!\"!!m#;!#!\"!!q#;!\"!\"!!t#;!$!\"!!y#;!#!\"!\"!#;!#!\"!\"%#;!#!\"!\"*#;!$!\"!\"/#;!#!\"!\":#;!#!\"!\">#;!\"!\"!\"A#;!$!\"!\"F#;!#!\"!\"K#;!#!\"!\"Q#;!\"!\"!\"\\#;!#!\"!\"l#;!\"!\"!\"t#;!$!\"!\"y#;!#!\"!#!#;!\"!\"!#'#;!#!\"!#+#;!\"!\"!#/#;!$!\"!!3#<!\"!\"!!7#<!\"!\"!!<#<!#!\"!!B#<!\"!\"!!G#<!\"!\"!!L#<!#!\"!!R#<!#!\"!!_#<!#!\"!!l#<!#!\"!!t#<!#!\"!!z#<!\"!\"!!~#<!#!\"!\"&#<!#!\"!\"*#<!#!\"!\"0#<!\"!\"!\"9#<!#!\"!\"A#<!#!\"!\"G#<!\"!\"!\"K#<!#!\"!\"Q#<!\"!\"!\"^#<!#!\"!\"l#<!&!\"!\"t#<!#!\"!\"z#<!\"!\"!#!#<!\"!\"!#&#<!#!\"!#,#<!\"!\"!#/#<!#!\"!"
  "!2#=!#!\"!!7#=!#!\"!!<#=!#!\"!!B#=!\"!\"!!G#=!\"!\"!!L#=!\"!\"!!S#=!\"!\"!!`#=!$!\"!!l#=!\"!\"!!t#=!#!\"!!z#=!\"!\"!!~#=!\"!\"!\"'#=!\"!\"!\"*#=!#!\"!\"0#=!\"!\"!\"9#=!\"!\"!\"A#=!#!\"!\"G#=!\"!\"!\"K#=!#!\"!\"Q#=!\"!\"!\"_#=!$!\"!\"l#=!\"!\"!\"t#=!#!\"!\"z#=!\"!\"!#!#=!\"!\"!#&#=!\"!\"!#,#=!\"!\"!#/#=!#!\"!!2#>!(!\"!!<#>!#!\"!!B#>!\"!\"!!G#>!\"!\"!!L#>!\"!\"!!S#>!\"!\"!!a#>!#!\"!!l#>!\"!\"!!t#>!#!\"!!z#>!\"!\"!!~#>!\"!\"!\"'#>!\"!\"!\"*#>!#!\"!\"0#>!\"!\"!\"9#>!\"!\"!\"A#>!#!\"!\"G#>!\"!\"!\"K#>!#!\"!\"Q#>!\"!\"!\"`#>!#!\"!\"l#>!\"!\"!\"t#>!#!\"!\"z#>!\"!\"!#!#>!\"!\"!#&#>!(!\"!#/#>!#!\"!!2#?!\"!\"!!8#?!\"!\"!!<#?!#!\"!!B#?!\"!\"!!G#?!\"!\"!!L#?!\"!\"!!S#?!\"!\"!!_#?!#!\"!!l#?!\"!\"!!t#?!#!\"!!z#?!\"!\"!!~#?!\"!\"!\"'#?!\"!\"!\"*#?!#!\"!\"0#?!\"!\"!\"9#?!\"!\"!\"A#?!#!\"!\"G#?!\"!\"!\"K#?!#!\"!\"Q#?!\"!\"!\"^#?!#!\"!\"l#?!\"!\"!\"t#?!#!\"!\"z#?!\"!\"!#!#?!\"!\"!#&#?!\"!\"!#/#?!#!\"!!1#@!#!\"!!8#@!#!\"!!<#@!#!\"!!B#@!\"!\"!!G#@!\"!\"!!L#@!#!\"!!R#@!#!\"!!]#@!#!\"!!l#@!#!\"!!t#@!#!\"!!z#@!\"!\"!!~#@!#!\"!\"&#@!#!\"!\"*#@!#!\"!\"0#@!\"!\"!\"9#@!#!\"!\"A#@!#!\"!\"G#@!\"!\"!\"K#@!#!\"!"
  "\"Q#@!\"!\"!\"\\#@!#!\"!\"l#@!\"!\"!\"t#@!#!\"!\"z#@!\"!\"!#!#@!\"!\"!#&#@!#!\"!#/#@!#!\"!!1#A!\"!\"!!9#A!\"!\"!!=#A!#!\"!!A#A!#!\"!!G#A!#!\"!!M#A!#!\"!!Q#A!#!\"!!\\#A!\"!\"!!m#A!#!\"!!q#A!\"!\"!!t#A!#!\"!!z#A!\"!\"!\"!#A!#!\"!\"%#A!#!\"!\"*#A!#!\"!\"0#A!\"!\"!\":#A!#!\"!\">#A!\"!\"!\"A#A!#!\"!\"G#A!\"!\"!\"L#A!\"!\"!\"P#A!#!\"!\"[#A!\"!\"!\"l#A!\"!\"!\"t#A!#!\"!\"z#A!\"!\"!#!#A!#!\"!#'#A!#!\"!#,#A!\"!\"!#/#A!#!\"!#5#A!#!\"!!0#B!#!\"!!9#B!#!\"!!>#B!$!\"!!B#B!\"!\"!!H#B!#!\"!!N#B!%!\"!!n#B!%!\"!!t#B!#!\"!!z#B!\"!\"!\"\"#B!%!\"!\"*#B!#!\"!\"0#B!\"!\"!\";#B!%!\"!\"A#B!#!\"!\"G#B!\"!\"!\"M#B!$!\"!\"Q#B!\"!\"!\"l#B!'!\"!\"t#B!#!\"!\"z#B!\"!\"!#\"#B!#!\"!#(#B!%!\"!#/#B!#!\"!#5#B!#!\"!\"##D!#!\"!\"##E!#!\"!$L#V!\"!\"!$R#V!\"!\"!$T#V!\"!\"!$f#V!\"!\"!$t#V!\"!\"!$z#V!\"!\"!%##V!&!\"!%,#V!\"!\"!%1#V!\"!\"!%3#V!\"!\"!%9#V!(!\"!%C#V!\"!\"!%G#V!\"!\"!$L#W!\"!\"!$Q#W!\"!\"!$T#W!\"!\"!$f#W!\"!\"!$t#W!#!\"!$y#W!#!\"!%%#W!\"!\"!%,#W!#!\"!%1#W!\"!\"!%3#W!\"!\"!%<#W!\"!\"!%C#W!#!\"!%G#W!\"!\"!$;#X!\"!\"!$>#X!\"!\"!$A#X!\"!\"!$C#X!$!\"!$I#X!%!\"!$N#X!\"!\"!$P#X!$!\"!$T#X!\"!\"!$V#X!$!\"!"
  "$[#X!\"!\"!$^#X!\"!\"!$a#X!\"!\"!$f#X!%!\"!$k#X!\"!\"!$o#X!\"!\"!$t#X!#!\"!$y#X!#!\"!$}#X!#!\"!%%#X!\"!\"!%,#X!\"!\"!%.#X!\"!\"!%1#X!\"!\"!%3#X!$!\"!%9#X!\"!\"!%<#X!\"!\"!%@#X!\"!\"!%D#X!\"!\"!%G#X!\"!\"!$>#Y!\"!\"!$A#Y!\"!\"!$C#Y!\"!\"!$F#Y!\"!\"!$I#Y!\"!\"!$L#Y!#!\"!$Q#Y!\"!\"!$T#Y!\"!\"!$V#Y!\"!\"!$Y#Y!\"!\"!$^#Y!\"!\"!$a#Y!\"!\"!$f#Y!\"!\"!$i#Y!\"!\"!$n#Y!\"!\"!$t#Y!\"!\"!$v#Y!\"!\"!$x#Y!\"!\"!$z#Y!\"!\"!$}#Y!\"!\"!%%#Y!\"!\"!%,#Y!\"!\"!%.#Y!\"!\"!%1#Y!\"!\"!%3#Y!\"!\"!%6#Y!\"!\"!%9#Y!\"!\"!%<#Y!\"!\"!%@#Y!\"!\"!%B#Y!\"!\"!%D#Y!\"!\"!%F#Y!\"!\"!$<#Z!\"!\"!$?#Z!\"!\"!$I#Z!\"!\"!$L#Z!#!\"!$Q#Z!\"!\"!$T#Z!\"!\"!$\\#Z!\"!\"!$_#Z!\"!\"!$f#Z!\"!\"!$l#Z!\"!\"!$n#Z!\"!\"!$t#Z!\"!\"!$v#Z!\"!\"!$x#Z!\"!\"!$z#Z!\"!\"!$}#Z!\"!\"!%%#Z!\"!\"!%,#Z!\"!\"!%/#Z!\"!\"!%1#Z!\"!\"!%3#Z!\"!\"!%6#Z!\"!\"!%9#Z!\"!\"!%<#Z!\"!\"!%@#Z!\"!\"!%B#Z!\"!\"!%F#Z!\"!\"!$<#[!#!\"!$?#[!#!\"!$C#[!\"!\"!$F#[!\"!\"!$I#[!\"!\"!$L#[!$!\"!$Q#[!\"!\"!$T#[!\"!\"!$V#[!\"!\"!$Y#[!\"!\"!$\\#[!#!\"!$_#[!#!\"!$f#[!\"!\"!$i#[!\"!\"!$l#[!\"!\"!$t#[!\"!\"!$w#[!\"!\"!$z#[!\"!\"!$}#[!\"!\"!%%#[!\"!\"!%,#[!\"!\"!%0#[!#!\"!"
  "%3#[!\"!\"!%6#[!\"!\"!%9#[!\"!\"!%<#[!\"!\"!%A#[!#!\"!%E#[!#!\"!$<#\\!#!\"!$@#\\!\"!\"!$C#\\!$!\"!$I#\\!\"!\"!$L#\\!\"!\"!$N#\\!\"!\"!$Q#\\!\"!\"!$T#\\!\"!\"!$V#\\!$!\"!$\\#\\!#!\"!$`#\\!\"!\"!$f#\\!$!\"!$m#\\!\"!\"!$t#\\!\"!\"!$w#\\!\"!\"!$z#\\!\"!\"!$}#\\!\"!\"!%%#\\!\"!\"!%,#\\!\"!\"!%0#\\!#!\"!%3#\\!\"!\"!%6#\\!\"!\"!%9#\\!\"!\"!%<#\\!\"!\"!%A#\\!\"!\"!%E#\\!\"!\"!$m#]!\"!\"!$k#^!#!\"!!!!!&+!\"\"!!!\"&+!\"\"!!!#&+!\"\"!!!$&+!\"\"!0!{#<!\"##^!{#<!\"#!0!|!\"!\"##J!|!\"!\"##^!|!\"!\"#%x!|!\"!\"#!0!}!\"!\"##J!}!\"!\"##^!}!\"!\"#%x!}!\"!\"#!0!~!\"!\"##J!~!\"!\"##^!~!\"!\"#%x!~!\"!\"#!0\"!!\"!\"##J\"!!\"!\"##^\"!!\"!\"#%x\"!!\"!\"#!0\"\"!\"!\"##J\"\"!\"!\"##^\"\"!\"!\"#%x\"\"!\"!\"#!0\"#!\"!\"##J\"#!\"!\"##^\"#!\"!\"#%x\"#!\"!\"#!0\"$!\"!\"##J\"$!\"!\"##^\"$!\"!\"#%x\"$!\"!\"#!0\"%!\"!\"##J\"%!\"!\"##^\"%!\"!\"#%x\"%!\"!\"#!0\"&!\"!\"##J\"&!\"!\"##^\"&!\"!\"#%x\"&!\"!\"#!0\"'!\"!\"##J\"'!\"!\"##^\"'!\"!\"#%x\"'!\"!\"#!0\"(!\"!\"##J\"(!\"!\"##^\"(!\"!\"#%x\"(!\"!\"#!0\")!\"!\"##J\")!\"!\"##^\")!\"!\"#%x\")!\"!\"#!0\"*!\"!\"##J\"*!\"!\"##^\"*!\"!\"#%x\"*!\"!\"#!0\"+!\"!\"##J\"+!\"!\"##^\"+!\"!\"#%x\"+!\"!\"#!0\",!\"!\"##J\",!\"!\"##^\",!\"!\"#%x\",!\"!\"#"
  "!0\"-!\"!\"##J\"-!\"!\"##^\"-!\"!\"#%x\"-!\"!\"#!0\".!\"!\"##J\".!\"!\"##^\".!\"!\"#%x\".!\"!\"#!0\"/!\"!\"##J\"/!\"!\"##^\"/!\"!\"#%x\"/!\"!\"#!0\"0!\"!\"##J\"0!\"!\"##^\"0!\"!\"#%x\"0!\"!\"#!0\"1!\"!\"##J\"1!\"!\"##^\"1!\"!\"#%x\"1!\"!\"#!0\"2!\"!\"##J\"2!\"!\"##^\"2!\"!\"#%x\"2!\"!\"#!0\"3!\"!\"##J\"3!\"!\"##^\"3!\"!\"#%x\"3!\"!\"#!0\"4!\"!\"##J\"4!\"!\"##^\"4!\"!\"#%x\"4!\"!\"#!0\"5!\"!\"##J\"5!\"!\"##^\"5!\"!\"#%x\"5!\"!\"#!0\"6!\"!\"##J\"6!\"!\"##^\"6!\"!\"#%x\"6!\"!\"#!0\"7!\"!\"##J\"7!\"!\"##^\"7!\"!\"#%x\"7!\"!\"#!0\"8!\"!\"##J\"8!\"!\"##^\"8!\"!\"#%x\"8!\"!\"#!0\"9!\"!\"##J\"9!\"!\"##^\"9!\"!\"#%x\"9!\"!\"#!0\":!\"!\"##J\":!\"!\"##^\":!\"!\"#%x\":!\"!\"#!0\";!\"!\"##J\";!\"!\"##^\";!\"!\"#%x\";!\"!\"#!0\"<!\"!\"##J\"<!\"!\"##^\"<!\"!\"#%x\"<!\"!\"#!0\"=!\"!\"##J\"=!\"!\"##^\"=!\"!\"#%x\"=!\"!\"#!0\">!\"!\"##J\">!\"!\"##^\">!\"!\"#%x\">!\"!\"#!0\"?!\"!\"##J\"?!\"!\"##^\"?!\"!\"#%x\"?!\"!\"#!0\"@!\"!\"##J\"@!\"!\"##^\"@!\"!\"#%x\"@!\"!\"#!0\"A!\"!\"##J\"A!\"!\"##^\"A!\"!\"#%x\"A!\"!\"#!0\"B!\"!\"##J\"B!\"!\"##^\"B!\"!\"#%x\"B!\"!\"#!0\"C!\"!\"##J\"C!\"!\"##^\"C!\"!\"#%x\"C!\"!\"#!0\"D!\"!\"##J\"D!\"!\"##^\"D!\"!\"#%x\"D!\"!\"#!0\"E!\"!\"##J\"E!\"!\"##^\"E!\"!\"#%x\"E!\"!\"#"
  "!0\"F!\"!\"##J\"F!\"!\"##^\"F!\"!\"#%x\"F!\"!\"#!0\"G!\"!\"##J\"G!\"!\"##^\"G!\"!\"#%x\"G!\"!\"#!0\"H!\"!\"##J\"H!\"!\"##^\"H!\"!\"#%x\"H!\"!\"#!0\"I!\"!\"##J\"I!\"!\"##^\"I!\"!\"#%x\"I!\"!\"#!0\"J!\"!\"##J\"J!\"!\"##^\"J!\"!\"#%x\"J!\"!\"#!0\"K!\"!\"##J\"K!\"!\"##^\"K!\"!\"#%x\"K!\"!\"#!0\"L!\"!\"##J\"L!\"!\"##^\"L!\"!\"#%x\"L!\"!\"#!0\"M!\"!\"##J\"M!\"!\"##^\"M!\"!\"#%x\"M!\"!\"#!0\"N!\"!\"##J\"N!\"!\"##^\"N!\"!\"#%x\"N!\"!\"#!0\"O!\"!\"##J\"O!\"!\"##^\"O!\"!\"#%x\"O!\"!\"#!0\"P!\"!\"##J\"P!\"!\"##^\"P!\"!\"#%x\"P!\"!\"#!0\"Q!\"!\"##J\"Q!\"!\"##^\"Q!\"!\"#%x\"Q!\"!\"#!0\"R!\"!\"##J\"R!\"!\"##^\"R!\"!\"#%x\"R!\"!\"#!0\"S!\"!\"##J\"S!\"!\"##^\"S!\"!\"#%x\"S!\"!\"#!0\"T!\"!\"##J\"T!\"!\"##^\"T!\"!\"#%x\"T!\"!\"#!0\"U!\"!\"##J\"U!\"!\"##^\"U!\"!\"#%x\"U!\"!\"#!0\"V!\"!\"##J\"V!\"!\"##^\"V!\"!\"#%x\"V!\"!\"#!0\"W!\"!\"##J\"W!\"!\"##^\"W!\"!\"#%x\"W!\"!\"#!0\"X!\"!\"##J\"X!\"!\"##^\"X!\"!\"#%x\"X!\"!\"#!0\"Y!\"!\"##J\"Y!\"!\"##^\"Y!\"!\"#%x\"Y!\"!\"#!0\"Z!\"!\"##J\"Z!\"!\"##^\"Z!\"!\"#%x\"Z!\"!\"#!0\"[!\"!\"##J\"[!\"!\"##^\"[!\"!\"#%x\"[!\"!\"#!0\"\\!\"!\"##J\"\\!\"!\"##^\"\\!\"!\"#%x\"\\!\"!\"#!0\"]!\"!\"##J\"]!\"!\"##^\"]!\"!\"#%x\"]!\"!\"#!0\"^!\"!\"##J\"^!\"!\"##^\"^!\"!\"#%x\"^!\"!\"#"
  "!0\"_!\"!\"##J\"_!\"!\"##^\"_!\"!\"#%x\"_!\"!\"#!0\"`!\"!\"##J\"`!\"!\"##^\"`!\"!\"#%x\"`!\"!\"#!0\"a!\"!\"##J\"a!\"!\"##^\"a!\"!\"#%x\"a!\"!\"#!0\"b!\"!\"##J\"b!\"!\"##^\"b!\"!\"#%x\"b!\"!\"#!0\"c!\"!\"##J\"c!\"!\"##^\"c!\"!\"#%x\"c!\"!\"#!0\"d!\"!\"##J\"d!\"!\"##^\"d!\"!\"#%x\"d!\"!\"#!0\"e#<!\"##^\"e#<!\"#"
 ))
 (list 520 278 (strcat
  "!9!;!'!\"!!H!;!-!\"!!b!;!(!\"!!q!;!'!\"!\"!!;!.!\"!\"7!;!(!\"!\"H!;!'!\"!\"W!;!%!\"!\"^!;!2!\"!!8!<!(!\"!!H!<!/!\"!!_!<!,!\"!!o!<!+!\"!\"!!<!.!\"!\"5!<!,!\"!\"H!<!'!\"!\"W!<!%!\"!\"^!<!2!\"!!8!=!(!\"!!H!=!0!\"!!^!=!-!\"!!n!=!-!\"!\"!!=!.!\"!\"4!=!.!\"!\"H!=!(!\"!\"W!=!%!\"!\"^!=!2!\"!!8!>!)!\"!!H!>!'!\"!!O!>!)!\"!!]!>!.!\"!!n!>!.!\"!\"!!>!-!\"!\"3!>!0!\"!\"H!>!)!\"!\"W!>!%!\"!\"_!>!1!\"!!7!?!*!\"!!H!?!&!\"!!R!?!&!\"!!\\!?!(!\"!!i!?!\"!\"!!n!?!#!\"!!v!?!&!\"!\"!!?!&!\"!\"2!?!'!\"!\"=!?!'!\"!\"H!?!)!\"!\"W!?!%!\"!\"d!?!&!\"!!7!@!%!\"!!<!@!%!\"!!H!@!&!\"!!R!@!&!\"!![!@!'!\"!!n!@!\"!\"!!v!@!&!\"!\"!!@!&!\"!\"1!@!'!\"!\">!@!'!\"!\"H!@!*!\"!\"W!@!%!\"!\"d!@!&!\"!!6!A!&!\"!!<!A!&!\"!!H!A!&!\"!!R!A!&!\"!![!A!&!\"!!v!A!&!\"!\"!!A!&!\"!\"1!A!&!\"!\"?!A!&!\"!\"H!A!+!\"!\"W!A!%!\"!\"d!A!&!\"!!6!B!%!\"!!<!B!&!\"!!H!B!&!\"!!Q!B!&!\"!![!B!&!\"!!v!B!&!\"!\"!!B!&!\"!\"1!B!&!\"!\"?!B!&!\"!\"H!B!&!\"!\"N!B!%!\"!\"W!B!%!\"!\"d!B!&!\"!%S!B!\"!\"!%Z!B!\"!\"!%^!B!#!\"!%g!B!$!\"!%r!B!$!\"!!6!C!%!\"!!=!C!%!\"!!H!C!.!\"!!Z!C!&!\"!!v!C!&!\"!\"!!C!&!\"!\"0!C!&!\"!"
  "\"?!C!'!\"!\"H!C!&!\"!\"N!C!&!\"!\"W!C!%!\"!\"d!C!&!\"!%T!C!\"!\"!%Y!C!\"!\"!%]!C!$!\"!%f!C!\"!\"!%j!C!\"!\"!%q!C!\"!\"!!5!D!&!\"!!=!D!&!\"!!H!D!-!\"!!Z!D!&!\"!!u!D!&!\"!\"!!D!-!\"!\"0!D!&!\"!\"@!D!&!\"!\"H!D!&!\"!\"O!D!&!\"!\"W!D!%!\"!\"d!D!&!\"!%T!D!\"!\"!%Y!D!\"!\"!%_!D!\"!\"!%f!D!\"!\"!%j!D!\"!\"!%p!D!\"!\"!!5!E!&!\"!!=!E!&!\"!!H!E!/!\"!!Z!E!&!\"!!t!E!&!\"!\"!!E!-!\"!\"0!E!&!\"!\"@!E!&!\"!\"H!E!&!\"!\"P!E!%!\"!\"W!E!%!\"!\"d!E!&!\"!%T!E!#!\"!%Y!E!\"!\"!%_!E!\"!\"!%f!E!\"!\"!%j!E!\"!\"!%p!E!%!\"!!5!F!%!\"!!>!F!%!\"!!H!F!0!\"!!Z!F!&!\"!!r!F!'!\"!\"!!F!-!\"!\"0!F!&!\"!\"@!F!&!\"!\"H!F!&!\"!\"P!F!&!\"!\"W!F!%!\"!\"d!F!&!\"!%U!F!\"!\"!%X!F!\"!\"!%_!F!\"!\"!%f!F!\"!\"!%j!F!\"!\"!%p!F!\"!\"!%t!F!\"!\"!!4!G!&!\"!!>!G!&!\"!!H!G!&!\"!!R!G!'!\"!!Z!G!&!\"!!q!G!'!\"!\"!!G!-!\"!\"0!G!&!\"!\"?!G!&!\"!\"H!G!&!\"!\"Q!G!&!\"!\"W!G!%!\"!\"d!G!&!\"!%U!G!\"!\"!%X!G!\"!\"!%_!G!\"!\"!%g!G!%!\"!%p!G!\"!\"!%t!G!\"!\"!!4!H!0!\"!!H!H!&!\"!!S!H!&!\"!!Z!H!'!\"!!p!H!'!\"!\"!!H!&!\"!\"0!H!'!\"!\"?!H!&!\"!\"H!H!&!\"!\"R!H!%!\"!\"W!H!%!\"!\"d!H!&!\"!%U!H!%!\"!%_!H!\"!\"!%j!H!\"!\"!"
  "%p!H!\"!\"!%t!H!\"!\"!!4!I!1!\"!!H!I!&!\"!!S!I!&!\"!![!I!&!\"!!o!I!'!\"!\"!!I!&!\"!\"1!I!&!\"!\"?!I!&!\"!\"H!I!&!\"!\"R!I!*!\"!\"d!I!&!\"!%V!I!#!\"!%_!I!\"!\"!%c!I!\"!\"!%i!I!\"!\"!%m!I!\"!\"!%p!I!#!\"!%t!I!\"!\"!!3!J!2!\"!!H!J!&!\"!!S!J!&!\"!![!J!'!\"!!n!J!&!\"!\"!!J!&!\"!\"1!J!'!\"!\">!J!'!\"!\"H!J!&!\"!\"S!J!)!\"!\"d!J!&!\"!%V!J!#!\"!%_!J!\"!\"!%c!J!\"!\"!%f!J!$!\"!%m!J!\"!\"!%q!J!$!\"!!3!K!2!\"!!H!K!&!\"!!R!K!'!\"!!\\!K!(!\"!!i!K!\"!\"!!n!K!&!\"!\"!!K!&!\"!\"2!K!'!\"!\"<!K!(!\"!\"H!K!&!\"!\"T!K!(!\"!\"d!K!&!\"!!2!L!&!\"!!@!L!&!\"!!H!L!'!\"!!P!L!(!\"!!\\!L!/!\"!!m!L!/!\"!\"!!L!&!\"!\"2!L!1!\"!\"H!L!&!\"!\"T!L!(!\"!\"d!L!&!\"!!2!M!&!\"!!@!M!&!\"!!H!M!0!\"!!]!M!.!\"!!m!M!/!\"!\"!!M!&!\"!\"3!M!/!\"!\"H!M!&!\"!\"U!M!'!\"!\"d!M!&!\"!!2!N!&!\"!!@!N!&!\"!!H!N!/!\"!!^!N!-!\"!!m!N!/!\"!\"!!N!&!\"!\"5!N!,!\"!\"H!N!&!\"!\"V!N!&!\"!\"d!N!&!\"!!1!O!&!\"!!A!O!&!\"!!H!O!-!\"!!a!O!)!\"!!m!O!/!\"!\"!!O!&!\"!\"7!O!(!\"!\"H!O!&!\"!\"V!O!&!\"!\"d!O!&!\"!#\"!_!\"!\"!#R!_!\"!\"!#\\!_!#!\"!#l!_!$!\"!!1!`!*!\"!!?!`!&!\"!!E!`!#!\"!!N!`!#!\"!!R!`!#!\"!!Z!`!#!\"!"
  "!_!`!%!\"!!m!`!\"!\"!!s!`!#!\"!!|!`!&!\"!\"+!`!&!\"!\"L!`!#!\"!\"T!`!#!\"!\"b!`!#!\"!#\"!`!\"!\"!#?!`!\"!\"!#R!`!\"!\"!#]!`!#!\"!#a!`!#!\"!#k!`!#!\"!!5!a!#!\"!!=!a!$!\"!!C!a!\"!\"!!F!a!\"!\"!!N!a!\"!\"!!R!a!#!\"!!Z!a!#!\"!!_!a!\"!\"!!b!a!#!\"!!l!a!#!\"!!r!a!$!\"!!|!a!\"!\"!\"\"!a!#!\"!\")!a!$!\"!\"/!a!\"!\"!\"L!a!#!\"!\"T!a!#!\"!\"b!a!#!\"!#\"!a!\"!\"!#@!a!\"!\"!#O!a!&!\"!#Y!a!#!\"!#]!a!\"!\"!#a!a!#!\"!#k!a!#!\"!$(!a!\"!\"!!5!b!\"!\"!!<!b!#!\"!!F!b!#!\"!!N!b!\"!\"!!R!b!$!\"!!Z!b!#!\"!!c!b!#!\"!!l!b!\"!\"!!r!b!\"!\"!!t!b!\"!\"!!|!b!\"!\"!\"#!b!#!\"!\"(!b!#!\"!\"L!b!#!\"!\"T!b!#!\"!#\"!b!\"!\"!#@!b!#!\"!#R!b!\"!\"!#X!b!#!\"!#[!b!\"!\"!#k!b!\"!\"!$(!b!\"!\"!!5!c!\"!\"!!<!c!\"!\"!!F!c!#!\"!!M!c!#!\"!!R!c!\"!\"!!T!c!#!\"!!Z!c!#!\"!!c!c!#!\"!!k!c!#!\"!!r!c!\"!\"!!t!c!#!\"!!|!c!\"!\"!\"#!c!#!\"!\"(!c!\"!\"!\"L!c!#!\"!\"T!c!#!\"!#\"!c!\"!\"!#R!c!\"!\"!#k!c!\"!\"!$(!c!\"!\"!!5!d!\"!\"!!;!d!#!\"!!G!d!\"!\"!!M!d!\"!\"!!R!d!\"!\"!!U!d!\"!\"!!Z!d!#!\"!!c!d!#!\"!!k!d!\"!\"!!q!d!#!\"!!u!d!\"!\"!!|!d!\"!\"!\"#!d!#!\"!\"'!d!#!\"!\"L!d!#!\"!\"T!d!#!\"!\"X!d!#!\"!\"[!d!%!\"!"
  "\"b!d!#!\"!\"h!d!%!\"!\"p!d!&!\"!\"{!d!%!\"!#\"!d!\"!\"!#(!d!%!\"!#3!d!#!\"!#:!d!\"!\"!#>!d!&!\"!#M!d!%!\"!#R!d!\"!\"!#X!d!&!\"!#a!d!#!\"!#i!d!'!\"!#r!d!&!\"!#{!d!#!\"!#~!d!%!\"!$&!d!'!\"!!5!e!\"!\"!!;!e!\"!\"!!G!e!#!\"!!M!e!\"!\"!!R!e!\"!\"!!U!e!#!\"!!Z!e!#!\"!!b!e!#!\"!!k!e!\"!\"!!q!e!\"!\"!!u!e!#!\"!!|!e!\"!\"!\"\"!e!#!\"!\"'!e!\"!\"!\"A!e!\"!\"!\"L!e!#!\"!\"T!e!#!\"!\"X!e!$!\"!\"]!e!#!\"!\"b!e!#!\"!\"g!e!#!\"!\"l!e!\"!\"!\"o!e!#!\"!\"t!e!#!\"!\"z!e!#!\"!\"~!e!$!\"!#'!e!#!\"!#+!e!#!\"!#4!e!\"!\"!#9!e!#!\"!#>!e!\"!\"!#B!e!\"!\"!#L!e!#!\"!#P!e!$!\"!#W!e!#!\"!#\\!e!#!\"!#a!e!#!\"!#k!e!#!\"!#q!e!#!\"!#v!e!#!\"!#{!e!$!\"!$\"!e!#!\"!$(!e!#!\"!!5!f!\"!\"!!;!f!\"!\"!!G!f!#!\"!!L!f!#!\"!!R!f!\"!\"!!V!f!#!\"!!Z!f!#!\"!!_!f!%!\"!!j!f!#!\"!!q!f!\"!\"!!u!f!#!\"!!|!f!&!\"!\"'!f!\"!\"!\"B!f!\"!\"!\"L!f!#!\"!\"T!f!#!\"!\"X!f!#!\"!\"^!f!#!\"!\"b!f!#!\"!\"f!f!#!\"!\"n!f!#!\"!\"u!f!\"!\"!\"y!f!#!\"!#!!f!#!\"!#&!f!#!\"!#,!f!\"!\"!#4!f!#!\"!#9!f!#!\"!#B!f!#!\"!#K!f!#!\"!#Q!f!#!\"!#V!f!#!\"!#]!f!\"!\"!#a!f!#!\"!#k!f!\"!\"!#p!f!#!\"!#w!f!\"!\"!#{!f!#!\"!$#!f!#!\"!"
  "$(!f!\"!\"!!5!g!\"!\"!!;!g!\"!\"!!H!g!\"!\"!!L!g!\"!\"!!R!g!\"!\"!!W!g!\"!\"!!Z!g!#!\"!!b!g!#!\"!!j!g!\"!\"!!p!g!#!\"!!v!g!\"!\"!!|!g!\"!\"!\"\"!g!$!\"!\"'!g!\"!\"!\"8!g!-!\"!\"M!g!\"!\"!\"T!g!#!\"!\"X!g!#!\"!\"^!g!#!\"!\"b!g!#!\"!\"f!g!#!\"!\"n!g!\"!\"!\"u!g!#!\"!\"y!g!\"!\"!#\"!g!\"!\"!#&!g!\"!\"!#,!g!#!\"!#4!g!#!\"!#9!g!\"!\"!#?!g!&!\"!#K!g!\"!\"!#R!g!\"!\"!#V!g!\"!\"!#]!g!#!\"!#a!g!#!\"!#k!g!\"!\"!#p!g!\"!\"!#w!g!#!\"!#{!g!#!\"!$#!g!#!\"!$(!g!\"!\"!!5!h!\"!\"!!;!h!#!\"!!H!h!#!\"!!K!h!#!\"!!R!h!\"!\"!!W!h!#!\"!!Z!h!#!\"!!d!h!\"!\"!!i!h!#!\"!!p!h!)!\"!!|!h!\"!\"!\"$!h!\"!\"!\"'!h!#!\"!\"B!h!\"!\"!\"M!h!\"!\"!\"T!h!#!\"!\"X!h!#!\"!\"^!h!#!\"!\"b!h!#!\"!\"f!h!\"!\"!\"n!h!\"!\"!\"u!h!#!\"!\"y!h!\"!\"!#\"!h!\"!\"!#&!h!)!\"!#5!h!\"!\"!#8!h!#!\"!#=!h!$!\"!#B!h!#!\"!#K!h!\"!\"!#R!h!\"!\"!#V!h!\"!\"!#]!h!#!\"!#a!h!#!\"!#k!h!\"!\"!#p!h!\"!\"!#w!h!#!\"!#{!h!#!\"!$#!h!#!\"!$(!h!\"!\"!!5!i!\"!\"!!;!i!#!\"!!H!i!#!\"!!K!i!#!\"!!R!i!\"!\"!!X!i!\"!\"!!Z!i!#!\"!!d!i!\"!\"!!i!i!\"!\"!!o!i!#!\"!!v!i!#!\"!!|!i!\"!\"!\"$!i!\"!\"!\"'!i!#!\"!\"A!i!\"!\"!\"M!i!\"!\"!\"T!i!#!\"!\"X!i!#!\"!"
  "\"^!i!#!\"!\"b!i!#!\"!\"f!i!\"!\"!\"n!i!\"!\"!\"u!i!#!\"!\"y!i!\"!\"!#\"!i!\"!\"!#&!i!#!\"!#5!i!#!\"!#8!i!\"!\"!#=!i!\"!\"!#B!i!#!\"!#K!i!\"!\"!#R!i!\"!\"!#V!i!\"!\"!#]!i!#!\"!#a!i!#!\"!#k!i!\"!\"!#p!i!\"!\"!#w!i!#!\"!#{!i!#!\"!$#!i!#!\"!$(!i!\"!\"!!5!j!\"!\"!!<!j!#!\"!!I!j!$!\"!!R!j!\"!\"!!X!j!%!\"!!d!j!\"!\"!!i!j!\"!\"!!o!j!#!\"!!w!j!\"!\"!!|!j!\"!\"!\"$!j!\"!\"!\"(!j!#!\"!\"M!j!#!\"!\"T!j!\"!\"!\"X!j!#!\"!\"^!j!#!\"!\"b!j!#!\"!\"f!j!#!\"!\"n!j!#!\"!\"u!j!\"!\"!\"y!j!#!\"!#!!j!#!\"!#&!j!#!\"!#6!j!\"!\"!#8!j!\"!\"!#=!j!\"!\"!#B!j!#!\"!#K!j!#!\"!#Q!j!#!\"!#V!j!#!\"!#]!j!\"!\"!#a!j!#!\"!#k!j!\"!\"!#p!j!#!\"!#w!j!\"!\"!#{!j!#!\"!$#!j!#!\"!$(!j!\"!\"!!5!k!\"!\"!!=!k!$!\"!!C!k!\"!\"!!I!k!$!\"!!R!k!\"!\"!!Y!k!$!\"!!^!k!\"!\"!!b!k!$!\"!!h!k!#!\"!!o!k!\"!\"!!w!k!#!\"!!|!k!\"!\"!\"\"!k!$!\"!\")!k!$!\"!\"/!k!\"!\"!\"N!k!#!\"!\"R!k!$!\"!\"X!k!#!\"!\"^!k!#!\"!\"b!k!#!\"!\"g!k!#!\"!\"o!k!#!\"!\"t!k!#!\"!\"z!k!#!\"!\"~!k!$!\"!#'!k!#!\"!#,!k!\"!\"!#6!k!$!\"!#=!k!#!\"!#A!k!$!\"!#L!k!#!\"!#P!k!$!\"!#W!k!#!\"!#\\!k!#!\"!#a!k!#!\"!#k!k!\"!\"!#q!k!#!\"!#v!k!#!\"!#{!k!#!\"!"
  "$#!k!#!\"!$(!k!#!\"!!5!l!\"!\"!!>!l!'!\"!!J!l!#!\"!!R!l!\"!\"!!Z!l!#!\"!!^!l!&!\"!!h!l!\"!\"!!n!l!#!\"!!x!l!\"!\"!!|!l!&!\"!\"*!l!'!\"!\"O!l!%!\"!\"X!l!#!\"!\"^!l!#!\"!\"b!l!#!\"!\"h!l!%!\"!\"p!l!%!\"!\"{!l!$!\"!#\"!l!\"!\"!#(!l!%!\"!#6!l!#!\"!#>!l!$!\"!#B!l!#!\"!#M!l!$!\"!#R!l!\"!\"!#X!l!%!\"!#a!l!#!\"!#k!l!\"!\"!#r!l!%!\"!#{!l!#!\"!$#!l!#!\"!$)!l!#!\"!!g!m!#!\"!!g!n!\"!\"!!V\"4!%!\"!\"%\"4!%!\"!\";\"4!#!\"!\"C\"4!%!\"!$:\"4!#!\"!$J\"4!%!\"!!?\"5!)!\"!!V\"5!%!\"!\"%\"5!%!\"!\":\"5!%!\"!\"B\"5!&!\"!$&\"5!&!\"!$9\"5!%!\"!$J\"5!%!\"!!?\"6!*!\"!!V\"6!%!\"!\"%\"6!%!\"!\";\"6!#!\"!\"A\"6!'!\"!\"K\"6!\"!\"!$&\"6!&!\"!$:\"6!#!\"!$J\"6!%!\"!!?\"7!+!\"!!V\"7!%!\"!\"%\"7!%!\"!\"A\"7!%!\"!\"I\"7!$!\"!$&\"7!&!\"!$J\"7!%!\"!!?\"8!$!\"!!E\"8!%!\"!!V\"8!%!\"!\"%\"8!%!\"!\"A\"8!$!\"!\"I\"8!$!\"!$%\"8!(!\"!$J\"8!%!\"!!?\"9!$!\"!!F\"9!$!\"!!L\"9!'!\"!!V\"9!%!\"!![\"9!%!\"!!c\"9!%!\"!!h\"9!%!\"!!r\"9!&!\"!!|\"9!&!\"!\"%\"9!%!\"!\"*\"9!%!\"!\"2\"9!%!\"!\"7\"9!#!\"!\":\"9!%!\"!\"?\"9!1!\"!$%\"9!$!\"!$)\"9!$!\"!$1\"9!%!\"!$6\"9!#!\"!$9\"9!%!\"!$@\"9!'!\"!$J\"9!%!\"!!?\":!$!\"!!E\":!$!\"!!L\":!(!\"!!V\":!+!\"!"
  "!c\":!+!\"!!p\":!(!\"!!z\":!(!\"!\"%\":!+!\"!\"2\":!(!\"!\":\":!%!\"!\"?\":!1!\"!$$\":!%!\"!$)\":!$!\"!$1\":!(!\"!$9\":!%!\"!$@\":!(!\"!$J\":!%!\"!!?\";!)!\"!!L\";!\"!\"!!Q\";!$!\"!!V\";!+!\"!!c\";!+!\"!!p\";!$!\"!!v\";!\"!\"!!z\";!(!\"!\"%\";!+!\"!\"2\";!(!\"!\":\";!%!\"!\"A\";!$!\"!\"I\";!$!\"!$$\";!$!\"!$)\";!%!\"!$1\";!(!\"!$9\";!%!\"!$@\";!\"!\"!$E\";!$!\"!$J\";!%!\"!!?\"<!*!\"!!Q\"<!$!\"!!V\"<!%!\"!!]\"<!$!\"!!c\"<!%!\"!!j\"<!$!\"!!p\"<!$!\"!!y\"<!%!\"!\"%\"<!%!\"!\",\"<!$!\"!\"2\"<!&!\"!\":\"<!%!\"!\"A\"<!$!\"!\"I\"<!$!\"!$$\"<!$!\"!$*\"<!$!\"!$1\"<!&!\"!$9\"<!%!\"!$E\"<!$!\"!$J\"<!%!\"!!?\"=!+!\"!!M\"=!(!\"!!V\"=!%!\"!!]\"=!$!\"!!c\"=!%!\"!!j\"=!$!\"!!p\"=!&!\"!!y\"=!$!\"!\"%\"=!%!\"!\",\"=!$!\"!\"2\"=!%!\"!\":\"=!%!\"!\"A\"=!$!\"!\"I\"=!$!\"!$#\"=!%!\"!$*\"=!$!\"!$1\"=!%!\"!$9\"=!%!\"!$A\"=!(!\"!$J\"=!%!\"!!?\">!$!\"!!F\">!$!\"!!K\">!*!\"!!V\">!%!\"!!]\">!$!\"!!c\">!%!\"!!j\">!%!\"!!q\">!'!\"!!y\">!$!\"!\"%\">!%!\"!\",\">!$!\"!\"2\">!%!\"!\":\">!%!\"!\"A\">!$!\"!\"I\">!$!\"!$#\">!,!\"!$1\">!%!\"!$9\">!%!\"!$?\">!*!\"!$J\">!%!\"!!?\"?!$!\"!!F\"?!%!\"!!K\"?!$!\"!!Q\"?!$!\"!!V\"?!%!\"!!]\"?!$!\"!!c\"?!%!\"!"
  "!j\"?!%!\"!!r\"?!'!\"!!y\"?!$!\"!\"%\"?!%!\"!\",\"?!$!\"!\"2\"?!%!\"!\":\"?!%!\"!\"A\"?!$!\"!\"I\"?!$!\"!$#\"?!,!\"!$1\"?!%!\"!$9\"?!%!\"!$?\"?!$!\"!$E\"?!$!\"!$J\"?!%!\"!!?\"@!$!\"!!F\"@!%!\"!!K\"@!$!\"!!P\"@!%!\"!!V\"@!%!\"!!]\"@!$!\"!!c\"@!%!\"!!j\"@!%!\"!!t\"@!%!\"!!y\"@!%!\"!\"%\"@!%!\"!\",\"@!$!\"!\"2\"@!%!\"!\":\"@!%!\"!\"A\"@!$!\"!\"I\"@!%!\"!$\"\"@!-!\"!$1\"@!%!\"!$9\"@!%!\"!$?\"@!$!\"!$D\"@!%!\"!$J\"@!%!\"!!?\"A!+!\"!!K\"A!*!\"!!V\"A!%!\"!!]\"A!$!\"!!c\"A!%!\"!!j\"A!%!\"!!p\"A!\"!\"!!t\"A!%!\"!!y\"A!)!\"!\"%\"A!%!\"!\",\"A!$!\"!\"2\"A!%!\"!\":\"A!%!\"!\"A\"A!$!\"!\"I\"A!'!\"!$\"\"A!%!\"!$+\"A!%!\"!$1\"A!%!\"!$9\"A!%!\"!$?\"A!*!\"!$J\"A!%!\"!!?\"B!+!\"!!K\"B!&!\"!!Q\"B!$!\"!!V\"B!%!\"!!]\"B!$!\"!!c\"B!%!\"!!j\"B!%!\"!!p\"B!(!\"!!z\"B!(!\"!\"%\"B!%!\"!\",\"B!$!\"!\"2\"B!%!\"!\":\"B!%!\"!\"A\"B!$!\"!\"J\"B!&!\"!$\"\"B!$!\"!$+\"B!%!\"!$1\"B!%!\"!$9\"B!%!\"!$?\"B!&!\"!$E\"B!$!\"!$J\"B!%!\"!!?\"C!)!\"!!L\"C!$!\"!!Q\"C!$!\"!!V\"C!%!\"!!]\"C!$!\"!!c\"C!%!\"!!j\"C!%!\"!!p\"C!'!\"!!|\"C!&!\"!\"%\"C!%!\"!\",\"C!$!\"!\"2\"C!%!\"!\":\"C!%!\"!\"A\"C!$!\"!\"K\"C!$!\"!$!\"C!%!\"!$,\"C!$!\"!$1\"C!%!\"!$9\"C!%!\"!$@\"C!$!\"!"
  "$E\"C!$!\"!$J\"C!%!\"!!A\"T!%!\"!!J\"T!)!\"!!Y\"T!&!\"!!d\"T!&!\"!!n\"T!)!\"!!}\"T!%!\"!\"(\"T!$!\"!\"/\"T!$!\"!\"4\"T!$!\"!\";\"T!$!\"!\"N\"T!&!\"!\"Y\"T!)!\"!\"e\"T!$!\"!$%\"T!%!\"!$.\"T!)!\"!$=\"T!&!\"!$H\"T!&!\"!$U\"T!%!\"!$^\"T!(!\"!$k\"T!)!\"!$y\"T!%!\"!%%\"T!$!\"!%>\"T!&!\"!%I\"T!)!\"!%U\"T!$!\"!!A\"U!%!\"!!J\"U!*!\"!!X\"U!(!\"!!c\"U!(!\"!!n\"U!*!\"!!}\"U!%!\"!\"(\"U!$!\"!\"/\"U!$!\"!\"4\"U!%!\"!\";\"U!$!\"!\"M\"U!(!\"!\"Y\"U!)!\"!\"e\"U!$!\"!$%\"U!%!\"!$.\"U!*!\"!$<\"U!(!\"!$G\"U!(!\"!$U\"U!%!\"!$^\"U!)!\"!$k\"U!)!\"!$y\"U!%!\"!%%\"U!$!\"!%=\"U!(!\"!%I\"U!)!\"!%U\"U!$!\"!!@\"V!#!\"!!C\"V!#!\"!!J\"V!$!\"!!P\"V!$!\"!!W\"V!$!\"!!^\"V!\"!\"!!c\"V!\"!\"!!h\"V!$!\"!!n\"V!$!\"!!t\"V!$!\"!!|\"V!#!\"!\"!\"V!#!\"!\"(\"V!$!\"!\"/\"V!$!\"!\"4\"V!%!\"!\";\"V!$!\"!\"L\"V!$!\"!\"S\"V!\"!\"!\"Y\"V!#!\"!\"e\"V!$!\"!$$\"V!#!\"!$'\"V!#!\"!$.\"V!$!\"!$4\"V!$!\"!$;\"V!$!\"!$B\"V!\"!\"!$G\"V!\"!\"!$L\"V!$!\"!$T\"V!#!\"!$W\"V!#!\"!$^\"V!$!\"!$d\"V!$!\"!$m\"V!$!\"!$x\"V!#!\"!${\"V!#!\"!%%\"V!$!\"!%<\"V!$!\"!%C\"V!\"!\"!%I\"V!#!\"!%U\"V!$!\"!!@\"W!#!\"!!C\"W!$!\"!!J\"W!$!\"!!Q\"W!#!\"!!V\"W!$!\"!!h\"W!$!\"!!n\"W!$!\"!!u\"W!#!\"!"
  "!|\"W!#!\"!\"!\"W!$!\"!\"(\"W!$!\"!\"/\"W!$!\"!\"4\"W!&!\"!\";\"W!$!\"!\"L\"W!$!\"!\"Y\"W!#!\"!\"e\"W!$!\"!$$\"W!#!\"!$'\"W!$!\"!$.\"W!$!\"!$5\"W!#!\"!$:\"W!$!\"!$L\"W!$!\"!$T\"W!#!\"!$W\"W!$!\"!$^\"W!$!\"!$d\"W!$!\"!$m\"W!$!\"!$x\"W!#!\"!${\"W!$!\"!%%\"W!$!\"!%<\"W!$!\"!%I\"W!#!\"!%U\"W!$!\"!!@\"X!#!\"!!C\"X!$!\"!!J\"X!$!\"!!P\"X!$!\"!!V\"X!$!\"!!h\"X!$!\"!!n\"X!$!\"!!t\"X!$!\"!!|\"X!#!\"!\"!\"X!$!\"!\"(\"X!$!\"!\"/\"X!$!\"!\"4\"X!&!\"!\";\"X!$!\"!\"L\"X!%!\"!\"Y\"X!#!\"!\"e\"X!$!\"!$$\"X!#!\"!$'\"X!$!\"!$.\"X!$!\"!$4\"X!$!\"!$:\"X!$!\"!$L\"X!$!\"!$T\"X!#!\"!$W\"X!$!\"!$^\"X!$!\"!$d\"X!$!\"!$m\"X!$!\"!$x\"X!#!\"!${\"X!$!\"!%%\"X!$!\"!%<\"X!%!\"!%I\"X!#!\"!%U\"X!$!\"!!?\"Y!$!\"!!D\"Y!#!\"!!J\"Y!)!\"!!V\"Y!$!\"!!h\"Y!$!\"!!n\"Y!)!\"!!{\"Y!$!\"!\"\"\"Y!#!\"!\"(\"Y!+!\"!\"4\"Y!#!\"!\"7\"Y!$!\"!\";\"Y!$!\"!\"M\"Y!&!\"!\"Y\"Y!)!\"!\"e\"Y!$!\"!$#\"Y!$!\"!$(\"Y!#!\"!$.\"Y!)!\"!$:\"Y!$!\"!$L\"Y!$!\"!$S\"Y!$!\"!$X\"Y!#!\"!$^\"Y!$!\"!$d\"Y!$!\"!$m\"Y!$!\"!$w\"Y!$!\"!$|\"Y!#!\"!%%\"Y!$!\"!%=\"Y!&!\"!%I\"Y!)!\"!%U\"Y!$!\"!!?\"Z!#!\"!!D\"Z!$!\"!!J\"Z!*!\"!!V\"Z!$!\"!!g\"Z!$!\"!!n\"Z!*!\"!!{\"Z!#!\"!\"\"\"Z!$!\"!\"(\"Z!+!\"!"
  "\"4\"Z!#!\"!\"8\"Z!#!\"!\";\"Z!$!\"!\"N\"Z!'!\"!\"Y\"Z!)!\"!\"e\"Z!$!\"!$#\"Z!#!\"!$(\"Z!$!\"!$.\"Z!*!\"!$:\"Z!$!\"!$K\"Z!$!\"!$S\"Z!#!\"!$X\"Z!$!\"!$^\"Z!)!\"!$m\"Z!$!\"!$w\"Z!#!\"!$|\"Z!$!\"!%%\"Z!$!\"!%>\"Z!'!\"!%I\"Z!)!\"!%U\"Z!$!\"!!?\"[!#!\"!!D\"[!$!\"!!J\"[!$!\"!!P\"[!$!\"!!V\"[!$!\"!!f\"[!$!\"!!n\"[!$!\"!!t\"[!$!\"!!{\"[!#!\"!\"\"\"[!$!\"!\"(\"[!$!\"!\"/\"[!$!\"!\"4\"[!#!\"!\"8\"[!'!\"!\"P\"[!&!\"!\"Y\"[!#!\"!\"e\"[!$!\"!$#\"[!#!\"!$(\"[!$!\"!$.\"[!$!\"!$4\"[!$!\"!$:\"[!$!\"!$J\"[!$!\"!$S\"[!#!\"!$X\"[!$!\"!$^\"[!(!\"!$m\"[!$!\"!$w\"[!#!\"!$|\"[!$!\"!%%\"[!$!\"!%@\"[!&!\"!%I\"[!#!\"!%U\"[!$!\"!!>\"\\!*!\"!!J\"\\!$!\"!!Q\"\\!$!\"!!V\"\\!$!\"!!e\"\\!$!\"!!n\"\\!$!\"!!u\"\\!$!\"!!z\"\\!*!\"!\"(\"\\!$!\"!\"/\"\\!$!\"!\"4\"\\!#!\"!\"9\"\\!&!\"!\"R\"\\!$!\"!\"Y\"\\!#!\"!\"e\"\\!$!\"!$\"\"\\!*!\"!$.\"\\!$!\"!$5\"\\!$!\"!$:\"\\!$!\"!$I\"\\!$!\"!$R\"\\!*!\"!$^\"\\!$!\"!$c\"\\!$!\"!$m\"\\!$!\"!$v\"\\!*!\"!%%\"\\!$!\"!%B\"\\!$!\"!%I\"\\!#!\"!%U\"\\!$!\"!!>\"]!*!\"!!J\"]!$!\"!!Q\"]!$!\"!!V\"]!$!\"!!d\"]!$!\"!!n\"]!$!\"!!u\"]!$!\"!!z\"]!*!\"!\"(\"]!$!\"!\"/\"]!$!\"!\"4\"]!#!\"!\"9\"]!&!\"!\"S\"]!#!\"!\"Y\"]!#!\"!\"e\"]!$!\"!$\"\"]!*!\"!$.\"]!$!\"!"
  "$5\"]!$!\"!$:\"]!$!\"!$H\"]!$!\"!$R\"]!*!\"!$^\"]!$!\"!$c\"]!$!\"!$m\"]!$!\"!$v\"]!*!\"!%%\"]!$!\"!%C\"]!#!\"!%I\"]!#!\"!%U\"]!$!\"!!>\"^!#!\"!!E\"^!$!\"!!J\"^!$!\"!!P\"^!$!\"!!W\"^!$!\"!!^\"^!\"!\"!!c\"^!$!\"!!n\"^!$!\"!!t\"^!$!\"!!z\"^!#!\"!\"#\"^!$!\"!\"(\"^!$!\"!\"/\"^!$!\"!\"4\"^!#!\"!\"9\"^!&!\"!\"L\"^!\"!\"!\"R\"^!$!\"!\"Y\"^!#!\"!\"e\"^!$!\"!$\"\"^!#!\"!$)\"^!$!\"!$.\"^!$!\"!$4\"^!$!\"!$;\"^!$!\"!$B\"^!\"!\"!$G\"^!$!\"!$R\"^!#!\"!$Y\"^!$!\"!$^\"^!$!\"!$d\"^!$!\"!$m\"^!$!\"!$v\"^!#!\"!$}\"^!$!\"!%%\"^!$!\"!%<\"^!\"!\"!%B\"^!$!\"!%I\"^!#!\"!%U\"^!$!\"!!>\"_!#!\"!!E\"_!$!\"!!J\"_!*!\"!!X\"_!(!\"!!b\"_!*!\"!!n\"_!*!\"!!z\"_!#!\"!\"#\"_!$!\"!\"(\"_!$!\"!\"/\"_!$!\"!\"4\"_!#!\"!\":\"_!%!\"!\"L\"_!)!\"!\"Y\"_!)!\"!\"e\"_!)!\"!$\"\"_!#!\"!$)\"_!$!\"!$.\"_!*!\"!$<\"_!(!\"!$F\"_!*!\"!$R\"_!#!\"!$Y\"_!$!\"!$^\"_!$!\"!$d\"_!$!\"!$k\"_!)!\"!$v\"_!#!\"!$}\"_!$!\"!%%\"_!)!\"!%<\"_!)!\"!%I\"_!)!\"!%U\"_!)!\"!!=\"`!$!\"!!F\"`!#!\"!!J\"`!(!\"!!Y\"`!&!\"!!b\"`!*!\"!!n\"`!(!\"!!y\"`!$!\"!\"$\"`!#!\"!\"(\"`!$!\"!\"/\"`!$!\"!\"4\"`!#!\"!\":\"`!%!\"!\"M\"`!'!\"!\"Y\"`!)!\"!\"e\"`!)!\"!$!\"`!$!\"!$*\"`!#!\"!$.\"`!(!\"!$=\"`!&!\"!"
  "$F\"`!*!\"!$Q\"`!$!\"!$Z\"`!#!\"!$^\"`!$!\"!$e\"`!$!\"!$k\"`!)!\"!$u\"`!$!\"!$~\"`!#!\"!%%\"`!)!\"!%=\"`!'!\"!%I\"`!)!\"!%U\"`!)!\"!\"?\"c!-!\"!%/\"c!-!\"!\"?\"d!-!\"!%/\"d!-!\"!!{#+!$!\"!\"A#+!#!\"!\"s#+!\"!\"!#>#+!#!\"!$C#+!#!\"!$Z#+!#!\"!!3#,!&!\"!!C#,!\"!\"!!i#,!\"!\"!!z#,!#!\"!\"A#,!#!\"!\"s#,!\"!\"!\"|#,!\"!\"!#>#,!#!\"!#L#,!#!\"!#r#,!\"!\"!#}#,!#!\"!$C#,!#!\"!$J#,!#!\"!$Z#,!#!\"!!3#-!\"!\"!!7#-!#!\"!!?#-!#!\"!!B#-!\"!\"!!h#-!\"!\"!!o#-!\"!\"!!z#-!#!\"!\"7#-!\"!\"!\"A#-!#!\"!\"W#-!\"!\"!\"[#-!\"!\"!\"p#-!&!\"!\"{#-!\"!\"!#>#-!#!\"!#M#-!\"!\"!#s#-!\"!\"!#u#-!#!\"!#}#-!#!\"!$5#-!$!\"!$9#-!\"!\"!$C#-!#!\"!$I#-!\"!\"!$K#-!\"!\"!$Z#-!#!\"!!3#.!\"!\"!!8#.!#!\"!!>#.!\"!\"!!A#.!\"!\"!!g#.!\"!\"!!o#.!\"!\"!!z#.!\"!\"!\"7#.!\"!\"!\"A#.!#!\"!\"X#.!$!\"!\"s#.!\"!\"!\"z#.!#!\"!#>#.!#!\"!#L#.!#!\"!#t#.!#!\"!#w#.!\"!\"!$5#.!\"!\"!$7#.!$!\"!$C#.!#!\"!$I#.!\"!\"!$L#.!\"!\"!$Z#.!#!\"!!3#/!\"!\"!!8#/!#!\"!!o#/!\"!\"!!z#/!\"!\"!\"7#/!\"!\"!\"A#/!#!\"!\"s#/!\"!\"!#>#/!#!\"!$C#/!#!\"!$Z#/!#!\"!!3#0!\"!\"!!8#0!#!\"!!=#0!&!\"!!E#0!#!\"!!H#0!$!\"!!N#0!$!\"!!Z#0!#!\"!!]#0!%!\"!"
  "!d#0!#!\"!!j#0!#!\"!!m#0!'!\"!!x#0!'!\"!\"##0!&!\"!\",#0!#!\"!\"/#0!%!\"!\"5#0!'!\"!\"A#0!#!\"!\"D#0!%!\"!\"M#0!&!\"!\"W#0!&!\"!\"a#0!%!\"!\"n#0!%!\"!\"s#0!\"!\"!\"y#0!&!\"!#$#0!#!\"!#'#0!%!\"!#0#0!%!\"!#5#0!\"!\"!#>#0!#!\"!#A#0!%!\"!#J#0!&!\"!#R#0!#!\"!#U#0!%!\"!#^#0!%!\"!#c#0!\"!\"!#l#0!#!\"!#o#0!#!\"!#t#0!&!\"!#}#0!#!\"!$*#0!%!\"!$/#0!\"!\"!$5#0!&!\"!$C#0!#!\"!$I#0!%!\"!$P#0!#!\"!$S#0!%!\"!$Z#0!#!\"!$]#0!%!\"!!3#1!\"!\"!!7#1!#!\"!!=#1!\"!\"!!A#1!\"!\"!!E#1!$!\"!!J#1!%!\"!!P#1!#!\"!!Z#1!$!\"!!_#1!#!\"!!d#1!#!\"!!j#1!#!\"!!o#1!#!\"!!z#1!#!\"!\"\"#1!#!\"!\"'#1!#!\"!\",#1!$!\"!\"1#1!#!\"!\"7#1!#!\"!\"A#1!$!\"!\"F#1!#!\"!\"L#1!#!\"!\"Q#1!#!\"!\"W#1!\"!\"!\"[#1!\"!\"!\"`#1!#!\"!\"e#1!\"!\"!\"m#1!#!\"!\"q#1!$!\"!\"x#1!#!\"!\"}#1!#!\"!#$#1!$!\"!#)#1!#!\"!#/#1!#!\"!#3#1!$!\"!#>#1!%!\"!#D#1!#!\"!#J#1!\"!\"!#N#1!\"!\"!#R#1!$!\"!#W#1!#!\"!#]#1!#!\"!#a#1!$!\"!#l#1!$!\"!#s#1!#!\"!#x#1!#!\"!#}#1!#!\"!$)#1!#!\"!$-#1!$!\"!$4#1!#!\"!$9#1!#!\"!$C#1!#!\"!$H#1!#!\"!$L#1!#!\"!$P#1!$!\"!$U#1!#!\"!$Z#1!$!\"!$_#1!#!\"!!3#2!&!\"!!A#2!#!\"!!E#2!#!\"!"
  "!K#2!#!\"!!Q#2!\"!\"!!Z#2!#!\"!!`#2!#!\"!!d#2!#!\"!!j#2!#!\"!!o#2!\"!\"!!z#2!\"!\"!\"!#2!#!\"!\"(#2!\"!\"!\",#2!#!\"!\"2#2!#!\"!\"7#2!\"!\"!\"A#2!#!\"!\"G#2!#!\"!\"K#2!#!\"!\"R#2!\"!\"!\"[#2!#!\"!\"_#2!#!\"!\"l#2!#!\"!\"r#2!#!\"!\"w#2!#!\"!\"~#2!\"!\"!#$#2!#!\"!#*#2!#!\"!#.#2!#!\"!#4#2!#!\"!#>#2!#!\"!#E#2!\"!\"!#N#2!#!\"!#R#2!#!\"!#X#2!#!\"!#\\#2!#!\"!#b#2!#!\"!#l#2!#!\"!#r#2!#!\"!#y#2!\"!\"!#}#2!#!\"!$(#2!#!\"!$.#2!#!\"!$3#2!#!\"!$:#2!\"!\"!$C#2!#!\"!$G#2!#!\"!$M#2!\"!\"!$P#2!#!\"!$V#2!#!\"!$Z#2!#!\"!$`#2!#!\"!!3#3!\"!\"!!7#3!$!\"!!>#3!&!\"!!E#3!#!\"!!K#3!#!\"!!Q#3!\"!\"!!Z#3!#!\"!!`#3!#!\"!!d#3!#!\"!!j#3!#!\"!!o#3!\"!\"!!z#3!\"!\"!\"!#3!\"!\"!\"(#3!#!\"!\",#3!#!\"!\"2#3!#!\"!\"7#3!\"!\"!\"A#3!#!\"!\"G#3!#!\"!\"K#3!\"!\"!\"R#3!#!\"!\"X#3!&!\"!\"_#3!#!\"!\"l#3!\"!\"!\"s#3!\"!\"!\"w#3!\"!\"!\"~#3!#!\"!#$#3!#!\"!#*#3!#!\"!#.#3!\"!\"!#5#3!\"!\"!#>#3!#!\"!#E#3!#!\"!#K#3!&!\"!#R#3!#!\"!#X#3!#!\"!#\\#3!\"!\"!#c#3!\"!\"!#l#3!#!\"!#r#3!\"!\"!#y#3!#!\"!#}#3!#!\"!$(#3!\"!\"!$/#3!\"!\"!$3#3!\"!\"!$:#3!#!\"!$C#3!#!\"!$G#3!\"!\"!$M#3!#!\"!$P#3!#!\"!$V#3!#!\"!"
  "$Z#3!#!\"!$`#3!#!\"!!3#4!\"!\"!!9#4!\"!\"!!<#4!$!\"!!A#4!#!\"!!E#4!#!\"!!K#4!#!\"!!Q#4!\"!\"!!Z#4!#!\"!!`#4!#!\"!!d#4!#!\"!!j#4!#!\"!!o#4!\"!\"!!z#4!\"!\"!\"!#4!\"!\"!\"(#4!#!\"!\",#4!#!\"!\"2#4!#!\"!\"7#4!\"!\"!\"A#4!#!\"!\"G#4!#!\"!\"K#4!\"!\"!\"R#4!#!\"!\"V#4!$!\"!\"[#4!#!\"!\"_#4!\"!\"!\"l#4!\"!\"!\"s#4!\"!\"!\"w#4!\"!\"!\"~#4!#!\"!#$#4!#!\"!#*#4!#!\"!#.#4!\"!\"!#5#4!\"!\"!#>#4!#!\"!#E#4!#!\"!#I#4!$!\"!#N#4!#!\"!#R#4!#!\"!#X#4!#!\"!#\\#4!\"!\"!#c#4!\"!\"!#l#4!#!\"!#r#4!\"!\"!#y#4!#!\"!#}#4!#!\"!$(#4!\"!\"!$/#4!\"!\"!$3#4!\"!\"!$:#4!#!\"!$C#4!#!\"!$G#4!)!\"!$P#4!#!\"!$V#4!#!\"!$Z#4!#!\"!$`#4!#!\"!!3#5!\"!\"!!9#5!\"!\"!!<#5!\"!\"!!A#5!#!\"!!E#5!#!\"!!K#5!#!\"!!Q#5!\"!\"!!Z#5!#!\"!!`#5!#!\"!!d#5!#!\"!!j#5!#!\"!!o#5!\"!\"!!z#5!\"!\"!\"!#5!\"!\"!\"(#5!#!\"!\",#5!#!\"!\"2#5!#!\"!\"7#5!\"!\"!\"A#5!#!\"!\"G#5!#!\"!\"K#5!\"!\"!\"R#5!#!\"!\"V#5!\"!\"!\"[#5!#!\"!\"_#5!\"!\"!\"l#5!\"!\"!\"s#5!\"!\"!\"w#5!\"!\"!\"~#5!#!\"!#$#5!#!\"!#*#5!#!\"!#.#5!\"!\"!#5#5!\"!\"!#>#5!#!\"!#E#5!#!\"!#I#5!\"!\"!#N#5!#!\"!#R#5!#!\"!#X#5!#!\"!#\\#5!\"!\"!#c#5!\"!\"!#l#5!#!\"!#r#5!\"!\"!"
  "#y#5!#!\"!#}#5!#!\"!$(#5!\"!\"!$/#5!\"!\"!$3#5!\"!\"!$:#5!#!\"!$C#5!#!\"!$G#5!#!\"!$P#5!#!\"!$V#5!#!\"!$Z#5!#!\"!$`#5!#!\"!!3#6!\"!\"!!9#6!\"!\"!!<#6!\"!\"!!A#6!#!\"!!E#6!#!\"!!K#6!#!\"!!Q#6!\"!\"!!Z#6!#!\"!!`#6!#!\"!!d#6!#!\"!!j#6!#!\"!!o#6!\"!\"!!z#6!\"!\"!\"!#6!#!\"!\"(#6!\"!\"!\",#6!#!\"!\"2#6!#!\"!\"7#6!\"!\"!\"A#6!#!\"!\"G#6!#!\"!\"K#6!#!\"!\"R#6!\"!\"!\"V#6!\"!\"!\"[#6!#!\"!\"_#6!#!\"!\"l#6!#!\"!\"r#6!#!\"!\"w#6!#!\"!\"~#6!\"!\"!#$#6!#!\"!#*#6!#!\"!#.#6!#!\"!#4#6!#!\"!#>#6!#!\"!#E#6!\"!\"!#I#6!\"!\"!#N#6!#!\"!#R#6!#!\"!#X#6!#!\"!#\\#6!#!\"!#b#6!#!\"!#l#6!#!\"!#r#6!#!\"!#y#6!\"!\"!#}#6!#!\"!$(#6!#!\"!$.#6!#!\"!$3#6!#!\"!$:#6!\"!\"!$C#6!#!\"!$G#6!#!\"!$P#6!#!\"!$V#6!#!\"!$Z#6!#!\"!$`#6!#!\"!!3#7!\"!\"!!7#7!$!\"!!<#7!#!\"!!@#7!$!\"!!E#7!#!\"!!K#7!#!\"!!Q#7!\"!\"!!Z#7!#!\"!!`#7!#!\"!!e#7!#!\"!!i#7!$!\"!!o#7!#!\"!!z#7!\"!\"!\"\"#7!#!\"!\"'#7!#!\"!\",#7!#!\"!\"2#7!#!\"!\"7#7!#!\"!\"A#7!#!\"!\"G#7!#!\"!\"L#7!#!\"!\"Q#7!#!\"!\"V#7!#!\"!\"Z#7!$!\"!\"`#7!#!\"!\"m#7!#!\"!\"q#7!$!\"!\"x#7!#!\"!\"}#7!#!\"!#$#7!#!\"!#*#7!#!\"!#/#7!#!\"!#3#7!$!\"!"
  "#>#7!$!\"!#D#7!#!\"!#I#7!#!\"!#M#7!$!\"!#R#7!#!\"!#X#7!#!\"!#]#7!#!\"!#a#7!$!\"!#l#7!#!\"!#s#7!#!\"!#x#7!#!\"!#}#7!#!\"!$)#7!#!\"!$-#7!$!\"!$4#7!#!\"!$9#7!#!\"!$C#7!#!\"!$H#7!#!\"!$M#7!\"!\"!$P#7!#!\"!$V#7!#!\"!$Z#7!#!\"!$`#7!#!\"!$d#7!#!\"!!3#8!&!\"!!=#8!$!\"!!A#8!#!\"!!E#8!#!\"!!K#8!#!\"!!Q#8!\"!\"!!Z#8!#!\"!!`#8!#!\"!!f#8!$!\"!!j#8!#!\"!!p#8!#!\"!!z#8!\"!\"!\"##8!%!\"!\",#8!#!\"!\"2#8!#!\"!\"8#8!#!\"!\"A#8!#!\"!\"G#8!#!\"!\"M#8!%!\"!\"W#8!$!\"!\"[#8!#!\"!\"a#8!%!\"!\"n#8!$!\"!\"s#8!\"!\"!\"y#8!%!\"!#$#8!#!\"!#*#8!#!\"!#0#8!$!\"!#5#8!\"!\"!#>#8!#!\"!#A#8!%!\"!#J#8!$!\"!#N#8!#!\"!#R#8!#!\"!#X#8!#!\"!#^#8!$!\"!#c#8!\"!\"!#l#8!#!\"!#t#8!%!\"!#}#8!#!\"!$*#8!$!\"!$/#8!\"!\"!$5#8!%!\"!$C#8!#!\"!$I#8!%!\"!$P#8!#!\"!$V#8!#!\"!$Z#8!#!\"!$`#8!#!\"!$d#8!#!\"!#5#9!\"!\"!#c#9!\"!\"!$/#9!\"!\"!\"Y#:!\"!\"!#4#:!#!\"!#b#:!#!\"!$.#:!#!\"!$J#:!#!\"!\"Y#;!\"!\"!#/#;!\"!\"!#3#;!#!\"!#]#;!\"!\"!#a#;!#!\"!$)#;!\"!\"!$-#;!#!\"!$J#;!#!\"!#/#<!&!\"!#]#<!&!\"!$)#<!&!\"!!w#G!#!\"!\"E#G!#!\"!!6#H!#!\"!!w#H!#!\"!\"E#H!#!\"!\"q#H!'!\"!!5#I!$!\"!"
  "!I#I!\"!\"!!w#I!#!\"!\"E#I!#!\"!\"Q#I!#!\"!\"U#I!\"!\"!\"q#I!\"!\"!#&#I!\"!\"!!5#J!\"!\"!!7#J!\"!\"!!I#J!\"!\"!!w#J!#!\"!\"E#J!#!\"!\"P#J!\"!\"!\"S#J!#!\"!\"W#J!#!\"!\"q#J!\"!\"!#&#J!\"!\"!!5#K!\"!\"!!7#K!#!\"!!I#K!\"!\"!!w#K!#!\"!\"E#K!#!\"!\"W#K!#!\"!\"q#K!\"!\"!#&#K!\"!\"!!4#L!#!\"!!8#L!\"!\"!!>#L!#!\"!!D#L!#!\"!!G#L!'!\"!!P#L!&!\"!!`#L!#!\"!!q#L!%!\"!!w#L!#!\"!!z#L!%!\"!\"%#L!&!\"!\".#L!#!\"!\"1#L!%!\"!\"?#L!%!\"!\"E#L!#!\"!\"H#L!%!\"!\"O#L!#!\"!\"U#L!$!\"!\"a#L!#!\"!\"q#L!\"!\"!\"y#L!#!\"!\"|#L!%!\"!#$#L!'!\"!#-#L!%!\"!#4#L!#!\"!#7#L!#!\"!!4#M!\"!\"!!8#M!#!\"!!>#M!#!\"!!D#M!#!\"!!I#M!#!\"!!O#M!#!\"!!T#M!#!\"!!a#M!$!\"!!p#M!#!\"!!u#M!\"!\"!!w#M!$!\"!!|#M!#!\"!\"$#M!#!\"!\")#M!#!\"!\".#M!$!\"!\"3#M!#!\"!\">#M!#!\"!\"C#M!\"!\"!\"E#M!$!\"!\"J#M!#!\"!\"O#M!#!\"!\"U#M!#!\"!\"b#M!$!\"!\"q#M!\"!\"!\"y#M!$!\"!\"~#M!#!\"!#&#M!#!\"!#,#M!#!\"!#0#M!#!\"!#4#M!$!\"!!4#N!\"!\"!!8#N!#!\"!!>#N!#!\"!!D#N!#!\"!!I#N!\"!\"!!N#N!#!\"!!U#N!\"!\"!!c#N!#!\"!!o#N!#!\"!!w#N!#!\"!!}#N!#!\"!\"##N!#!\"!\"*#N!\"!\"!\".#N!#!\"!\"4#N!#!\"!\"=#N!#!\"!\"E#N!#!\"!\"K#N!#!\"!\"O#N!#!\"!"
  "\"U#N!#!\"!\"d#N!#!\"!\"q#N!'!\"!\"y#N!#!\"!#!#N!#!\"!#&#N!\"!\"!#+#N!#!\"!#1#N!\"!\"!#4#N!#!\"!!3#O!#!\"!!9#O!\"!\"!!>#O!#!\"!!D#O!#!\"!!I#O!\"!\"!!N#O!\"!\"!!U#O!#!\"!!e#O!#!\"!!o#O!#!\"!!w#O!#!\"!!}#O!#!\"!\"##O!\"!\"!\"*#O!#!\"!\".#O!#!\"!\"4#O!#!\"!\"=#O!#!\"!\"E#O!#!\"!\"K#O!#!\"!\"O#O!#!\"!\"U#O!#!\"!\"f#O!#!\"!\"q#O!\"!\"!\"y#O!#!\"!#!#O!#!\"!#&#O!\"!\"!#+#O!\"!\"!#1#O!#!\"!#4#O!#!\"!!3#P!)!\"!!>#P!#!\"!!D#P!#!\"!!I#P!\"!\"!!N#P!\"!\"!!U#P!#!\"!!d#P!#!\"!!o#P!\"!\"!!w#P!#!\"!!}#P!#!\"!\"##P!\"!\"!\"*#P!#!\"!\".#P!#!\"!\"4#P!#!\"!\"=#P!\"!\"!\"E#P!#!\"!\"K#P!#!\"!\"O#P!#!\"!\"U#P!#!\"!\"e#P!#!\"!\"q#P!\"!\"!\"y#P!#!\"!#!#P!#!\"!#&#P!\"!\"!#+#P!)!\"!#4#P!#!\"!!2#Q!#!\"!!9#Q!#!\"!!>#Q!#!\"!!D#Q!#!\"!!I#Q!\"!\"!!N#Q!\"!\"!!U#Q!#!\"!!b#Q!#!\"!!o#Q!\"!\"!!w#Q!#!\"!!}#Q!#!\"!\"##Q!\"!\"!\"*#Q!#!\"!\".#Q!#!\"!\"4#Q!#!\"!\"=#Q!\"!\"!\"E#Q!#!\"!\"K#Q!#!\"!\"O#Q!#!\"!\"U#Q!#!\"!\"c#Q!#!\"!\"q#Q!\"!\"!\"y#Q!#!\"!#!#Q!#!\"!#&#Q!\"!\"!#+#Q!#!\"!#4#Q!#!\"!!2#R!#!\"!!:#R!\"!\"!!>#R!#!\"!!D#R!#!\"!!I#R!\"!\"!!N#R!#!\"!!U#R!\"!\"!!`#R!#!\"!!o#R!#!\"!!w#R!#!\"!"
  "!}#R!#!\"!\"##R!#!\"!\"*#R!\"!\"!\".#R!#!\"!\"4#R!#!\"!\"=#R!#!\"!\"E#R!#!\"!\"K#R!#!\"!\"O#R!#!\"!\"U#R!#!\"!\"a#R!#!\"!\"q#R!\"!\"!\"y#R!#!\"!#!#R!#!\"!#&#R!\"!\"!#+#R!#!\"!#4#R!#!\"!!2#S!\"!\"!!:#S!#!\"!!?#S!#!\"!!C#S!$!\"!!I#S!#!\"!!O#S!#!\"!!T#S!#!\"!!_#S!\"!\"!!p#S!#!\"!!w#S!#!\"!!}#S!#!\"!\"$#S!#!\"!\")#S!#!\"!\".#S!#!\"!\"4#S!#!\"!\">#S!#!\"!\"E#S!#!\"!\"K#S!#!\"!\"P#S!#!\"!\"T#S!$!\"!\"`#S!\"!\"!\"q#S!\"!\"!\"y#S!#!\"!#!#S!#!\"!#&#S!#!\"!#,#S!#!\"!#1#S!\"!\"!#4#S!#!\"!#:#S!#!\"!!1#T!#!\"!!;#T!\"!\"!!@#T!$!\"!!D#T!#!\"!!J#T!#!\"!!P#T!%!\"!!q#T!%!\"!!w#T!#!\"!!}#T!#!\"!\"%#T!%!\"!\".#T!#!\"!\"4#T!#!\"!\"?#T!%!\"!\"E#T!#!\"!\"K#T!#!\"!\"Q#T!$!\"!\"U#T!#!\"!\"q#T!'!\"!\"y#T!#!\"!#!#T!#!\"!#'#T!#!\"!#-#T!%!\"!#4#T!#!\"!#:#T!#!\"!\"'#V!\"!\"!\"'#W!\"!\"!$h#j!\"!\"!$n#j!$!\"!%&#j!\"!\"!%4#j!\"!\"!%;#j!\"!\"!%C#j!'!\"!%M#j!\"!\"!%R#j!\"!\"!%U#j!\"!\"!%[#j!\"!\"!%]#j!(!\"!%h#j!\"!\"!%l#j!\"!\"!$h#k!\"!\"!$m#k!\"!\"!$p#k!\"!\"!%&#k!\"!\"!%4#k!#!\"!%:#k!#!\"!%E#k!\"!\"!%M#k!#!\"!%R#k!\"!\"!%U#k!\"!\"!%_#k!\"!\"!%d#k!\"!\"!%g#k!#!\"!%l#k!\"!\"!$U#l!\"!\"!"
  "$Y#l!\"!\"!$\\#l!\"!\"!$_#l!$!\"!$d#l!$!\"!$h#l!\"!\"!$k#l!%!\"!$p#l!\"!\"!$t#l!$!\"!$x#l!\"!\"!$|#l!\"!\"!%!#l!\"!\"!%&#l!%!\"!%+#l!\"!\"!%/#l!\"!\"!%4#l!#!\"!%:#l!#!\"!%>#l!$!\"!%E#l!\"!\"!%M#l!\"!\"!%O#l!\"!\"!%R#l!\"!\"!%U#l!%!\"!%[#l!\"!\"!%_#l!\"!\"!%d#l!\"!\"!%g#l!#!\"!$X#m!#!\"!$^#m!\"!\"!$a#m!#!\"!$d#m!\"!\"!$h#m!\"!\"!$j#m!\"!\"!$m#m!\"!\"!$p#m!\"!\"!$s#m!\"!\"!$v#m!#!\"!${#m!#!\"!%&#m!\"!\"!%*#m!\"!\"!%,#m!\"!\"!%/#m!\"!\"!%4#m!\"!\"!%6#m!\"!\"!%9#m!\"!\"!%;#m!\"!\"!%>#m!\"!\"!%E#m!\"!\"!%M#m!\"!\"!%O#m!\"!\"!%R#m!\"!\"!%U#m!\"!\"!%X#m!\"!\"!%[#m!\"!\"!%_#m!\"!\"!%d#m!\"!\"!%g#m!\"!\"!%i#m!\"!\"!%k#m!\"!\"!$V#n!\"!\"!$X#n!#!\"!$[#n!\"!\"!$^#n!\"!\"!$b#n!\"!\"!$d#n!\"!\"!$h#n!#!\"!$m#n!\"!\"!$p#n!\"!\"!$s#n!\"!\"!$w#n!\"!\"!$y#n!\"!\"!${#n!#!\"!$~#n!\"!\"!%&#n!\"!\"!%*#n!\"!\"!%,#n!\"!\"!%4#n!\"!\"!%6#n!\"!\"!%9#n!\"!\"!%;#n!\"!\"!%>#n!\"!\"!%E#n!\"!\"!%M#n!\"!\"!%P#n!\"!\"!%R#n!\"!\"!%U#n!\"!\"!%[#n!\"!\"!%_#n!\"!\"!%d#n!\"!\"!%i#n!\"!\"!%k#n!\"!\"!$V#o!\"!\"!$[#o!\"!\"!$b#o!\"!\"!$d#o!\"!\"!$h#o!#!\"!$m#o!\"!\"!$p#o!\"!\"!$w#o!\"!\"!$y#o!\"!\"!$~#o!\"!\"!"
  "%&#o!\"!\"!%*#o!\"!\"!%,#o!\"!\"!%.#o!\"!\"!%4#o!\"!\"!%7#o!#!\"!%;#o!\"!\"!%>#o!\"!\"!%E#o!\"!\"!%M#o!\"!\"!%Q#o!#!\"!%U#o!\"!\"!%[#o!\"!\"!%_#o!\"!\"!%e#o!#!\"!%i#o!\"!\"!%k#o!\"!\"!$V#p!#!\"!$Z#p!#!\"!$^#p!\"!\"!$a#p!#!\"!$d#p!\"!\"!$h#p!\"!\"!$j#p!\"!\"!$m#p!\"!\"!$p#p!\"!\"!$s#p!\"!\"!$v#p!#!\"!$y#p!#!\"!$}#p!#!\"!%&#p!\"!\"!%)#p!#!\"!%-#p!#!\"!%4#p!\"!\"!%7#p!#!\"!%;#p!\"!\"!%>#p!\"!\"!%B#p!\"!\"!%E#p!\"!\"!%M#p!\"!\"!%Q#p!#!\"!%U#p!\"!\"!%[#p!\"!\"!%_#p!\"!\"!%e#p!#!\"!%i#p!#!\"!$W#q!\"!\"!$Z#q!\"!\"!$_#q!$!\"!$d#q!\"!\"!$h#q!\"!\"!$k#q!\"!\"!$m#q!\"!\"!$p#q!\"!\"!$t#q!$!\"!$z#q!\"!\"!$}#q!\"!\"!%&#q!%!\"!%-#q!\"!\"!%4#q!\"!\"!%;#q!\"!\"!%>#q!\"!\"!%B#q!\"!\"!%E#q!\"!\"!%M#q!\"!\"!%R#q!\"!\"!%U#q!\"!\"!%[#q!\"!\"!%_#q!\"!\"!%e#q!#!\"!%j#q!\"!\"!%-#r!\"!\"!%+#s!#!\"!!!!!&S!\"\"!!!\"&S!\"\"!!!#&S!\"\"!!!$&S!\"\"!1\"%#N!\"##s\"%#N!\"#!1\"&!\"!\"##]\"&!\"!\"##s\"&!\"!\"#&A\"&!\"!\"#!1\"'!\"!\"##]\"'!\"!\"##s\"'!\"!\"#&A\"'!\"!\"#!1\"(!\"!\"##]\"(!\"!\"##s\"(!\"!\"#&A\"(!\"!\"#!1\")!\"!\"##]\")!\"!\"##s\")!\"!\"#&A\")!\"!\"#!1\"*!\"!\"##]\"*!\"!\"##s\"*!\"!\"#&A\"*!\"!\"#!1\"+!\"!\"#"
  "#]\"+!\"!\"##s\"+!\"!\"#&A\"+!\"!\"#!1\",!\"!\"##]\",!\"!\"##s\",!\"!\"#&A\",!\"!\"#!1\"-!\"!\"##]\"-!\"!\"##s\"-!\"!\"#&A\"-!\"!\"#!1\".!\"!\"##]\".!\"!\"##s\".!\"!\"#&A\".!\"!\"#!1\"/!\"!\"##]\"/!\"!\"##s\"/!\"!\"#&A\"/!\"!\"#!1\"0!\"!\"##]\"0!\"!\"##s\"0!\"!\"#&A\"0!\"!\"#!1\"1!\"!\"##]\"1!\"!\"##s\"1!\"!\"#&A\"1!\"!\"#!1\"2!\"!\"##]\"2!\"!\"##s\"2!\"!\"#&A\"2!\"!\"#!1\"3!\"!\"##]\"3!\"!\"##s\"3!\"!\"#&A\"3!\"!\"#!1\"4!\"!\"##]\"4!\"!\"##s\"4!\"!\"#&A\"4!\"!\"#!1\"5!\"!\"##]\"5!\"!\"##s\"5!\"!\"#&A\"5!\"!\"#!1\"6!\"!\"##]\"6!\"!\"##s\"6!\"!\"#&A\"6!\"!\"#!1\"7!\"!\"##]\"7!\"!\"##s\"7!\"!\"#&A\"7!\"!\"#!1\"8!\"!\"##]\"8!\"!\"##s\"8!\"!\"#&A\"8!\"!\"#!1\"9!\"!\"##]\"9!\"!\"##s\"9!\"!\"#&A\"9!\"!\"#!1\":!\"!\"##]\":!\"!\"##s\":!\"!\"#&A\":!\"!\"#!1\";!\"!\"##]\";!\"!\"##s\";!\"!\"#&A\";!\"!\"#!1\"<!\"!\"##]\"<!\"!\"##s\"<!\"!\"#&A\"<!\"!\"#!1\"=!\"!\"##]\"=!\"!\"##s\"=!\"!\"#&A\"=!\"!\"#!1\">!\"!\"##]\">!\"!\"##s\">!\"!\"#&A\">!\"!\"#!1\"?!\"!\"##]\"?!\"!\"##s\"?!\"!\"#&A\"?!\"!\"#!1\"@!\"!\"##]\"@!\"!\"##s\"@!\"!\"#&A\"@!\"!\"#!1\"A!\"!\"##]\"A!\"!\"##s\"A!\"!\"#&A\"A!\"!\"#!1\"B!\"!\"##]\"B!\"!\"##s\"B!\"!\"#&A\"B!\"!\"#!1\"C!\"!\"##]\"C!\"!\"##s\"C!\"!\"#&A\"C!\"!\"#!1\"D!\"!\"#"
  "#]\"D!\"!\"##s\"D!\"!\"#&A\"D!\"!\"#!1\"E!\"!\"##]\"E!\"!\"##s\"E!\"!\"#&A\"E!\"!\"#!1\"F!\"!\"##]\"F!\"!\"##s\"F!\"!\"#&A\"F!\"!\"#!1\"G!\"!\"##]\"G!\"!\"##s\"G!\"!\"#&A\"G!\"!\"#!1\"H!\"!\"##]\"H!\"!\"##s\"H!\"!\"#&A\"H!\"!\"#!1\"I!\"!\"##]\"I!\"!\"##s\"I!\"!\"#&A\"I!\"!\"#!1\"J!\"!\"##]\"J!\"!\"##s\"J!\"!\"#&A\"J!\"!\"#!1\"K!\"!\"##]\"K!\"!\"##s\"K!\"!\"#&A\"K!\"!\"#!1\"L!\"!\"##]\"L!\"!\"##s\"L!\"!\"#&A\"L!\"!\"#!1\"M!\"!\"##]\"M!\"!\"##s\"M!\"!\"#&A\"M!\"!\"#!1\"N!\"!\"##]\"N!\"!\"##s\"N!\"!\"#&A\"N!\"!\"#!1\"O!\"!\"##]\"O!\"!\"##s\"O!\"!\"#&A\"O!\"!\"#!1\"P!\"!\"##]\"P!\"!\"##s\"P!\"!\"#&A\"P!\"!\"#!1\"Q!\"!\"##]\"Q!\"!\"##s\"Q!\"!\"#&A\"Q!\"!\"#!1\"R!\"!\"##]\"R!\"!\"##s\"R!\"!\"#&A\"R!\"!\"#!1\"S!\"!\"##]\"S!\"!\"##s\"S!\"!\"#&A\"S!\"!\"#!1\"T!\"!\"##]\"T!\"!\"##s\"T!\"!\"#&A\"T!\"!\"#!1\"U!\"!\"##]\"U!\"!\"##s\"U!\"!\"#&A\"U!\"!\"#!1\"V!\"!\"##]\"V!\"!\"##s\"V!\"!\"#&A\"V!\"!\"#!1\"W!\"!\"##]\"W!\"!\"##s\"W!\"!\"#&A\"W!\"!\"#!1\"X!\"!\"##]\"X!\"!\"##s\"X!\"!\"#&A\"X!\"!\"#!1\"Y!\"!\"##]\"Y!\"!\"##s\"Y!\"!\"#&A\"Y!\"!\"#!1\"Z!\"!\"##]\"Z!\"!\"##s\"Z!\"!\"#&A\"Z!\"!\"#!1\"[!\"!\"##]\"[!\"!\"##s\"[!\"!\"#&A\"[!\"!\"#!1\"\\!\"!\"##]\"\\!\"!\"##s\"\\!\"!\"#&A\"\\!\"!\"#!1\"]!\"!\"#"
  "#]\"]!\"!\"##s\"]!\"!\"#&A\"]!\"!\"#!1\"^!\"!\"##]\"^!\"!\"##s\"^!\"!\"#&A\"^!\"!\"#!1\"_!\"!\"##]\"_!\"!\"##s\"_!\"!\"#&A\"_!\"!\"#!1\"`!\"!\"##]\"`!\"!\"##s\"`!\"!\"#&A\"`!\"!\"#!1\"a!\"!\"##]\"a!\"!\"##s\"a!\"!\"#&A\"a!\"!\"#!1\"b!\"!\"##]\"b!\"!\"##s\"b!\"!\"#&A\"b!\"!\"#!1\"c!\"!\"##]\"c!\"!\"##s\"c!\"!\"#&A\"c!\"!\"#!1\"d!\"!\"##]\"d!\"!\"##s\"d!\"!\"#&A\"d!\"!\"#!1\"e!\"!\"##]\"e!\"!\"##s\"e!\"!\"#&A\"e!\"!\"#!1\"f!\"!\"##]\"f!\"!\"##s\"f!\"!\"#&A\"f!\"!\"#!1\"g!\"!\"##]\"g!\"!\"##s\"g!\"!\"#&A\"g!\"!\"#!1\"h!\"!\"##]\"h!\"!\"##s\"h!\"!\"#&A\"h!\"!\"#!1\"i!\"!\"##]\"i!\"!\"##s\"i!\"!\"#&A\"i!\"!\"#!1\"j!\"!\"##]\"j!\"!\"##s\"j!\"!\"#&A\"j!\"!\"#!1\"k!\"!\"##]\"k!\"!\"##s\"k!\"!\"#&A\"k!\"!\"#!1\"l!\"!\"##]\"l!\"!\"##s\"l!\"!\"#&A\"l!\"!\"#!1\"m!\"!\"##]\"m!\"!\"##s\"m!\"!\"#&A\"m!\"!\"#!1\"n!\"!\"##]\"n!\"!\"##s\"n!\"!\"#&A\"n!\"!\"#!1\"o!\"!\"##]\"o!\"!\"##s\"o!\"!\"#&A\"o!\"!\"#!1\"p!\"!\"##]\"p!\"!\"##s\"p!\"!\"#&A\"p!\"!\"#!1\"q!\"!\"##]\"q!\"!\"##s\"q!\"!\"#&A\"q!\"!\"#!1\"r!\"!\"##]\"r!\"!\"##s\"r!\"!\"#&A\"r!\"!\"#!1\"s#N!\"##s\"s#N!\"#"
 ))
 (list 600 321 (strcat
  "!<!>!(!\"!!O!>!.!\"!!m!>!)!\"!!~!>!(!\"!\"1!>!/!\"!\"J!>!(!\"!\"^!>!'!\"!\"o!>!&!\"!\"x!>!4!\"!!<!?!(!\"!!O!?!0!\"!!j!?!-!\"!!|!?!,!\"!\"1!?!/!\"!\"G!?!.!\"!\"^!?!(!\"!\"o!?!&!\"!\"x!?!4!\"!!;!@!*!\"!!O!@!1!\"!!h!@!/!\"!!{!@!.!\"!\"1!@!/!\"!\"F!@!0!\"!\"^!@!(!\"!\"o!@!&!\"!\"x!@!4!\"!!;!A!*!\"!!O!A!2!\"!!g!A!0!\"!!{!A!/!\"!\"1!A!/!\"!\"E!A!2!\"!\"^!A!)!\"!\"o!A!&!\"!\"x!A!4!\"!!:!B!+!\"!!O!B!&!\"!!Y!B!(!\"!!f!B!1!\"!!{!B!%!\"!\"$!B!(!\"!\"1!B!.!\"!\"D!B!4!\"!\"^!B!*!\"!\"o!B!&!\"!\"x!B!4!\"!!:!C!,!\"!!O!C!&!\"!!Z!C!'!\"!!e!C!)!\"!!u!C!\"!\"!!{!C!#!\"!\"&!C!'!\"!\"1!C!&!\"!\"C!C!(!\"!\"P!C!)!\"!\"^!C!*!\"!\"o!C!&!\"!\"~!C!'!\"!!:!D!&!\"!!@!D!&!\"!!O!D!&!\"!!Z!D!'!\"!!e!D!(!\"!\"&!D!'!\"!\"1!D!&!\"!\"C!D!'!\"!\"R!D!'!\"!\"^!D!+!\"!\"o!D!&!\"!\"~!D!'!\"!!9!E!&!\"!!@!E!&!\"!!O!E!&!\"!!Z!E!'!\"!!d!E!(!\"!\"&!E!'!\"!\"1!E!&!\"!\"B!E!'!\"!\"R!E!(!\"!\"^!E!,!\"!\"o!E!&!\"!\"~!E!'!\"!!9!F!&!\"!!@!F!'!\"!!O!F!&!\"!!Z!F!&!\"!!d!F!'!\"!\"&!F!'!\"!\"1!F!&!\"!\"B!F!'!\"!\"S!F!'!\"!\"^!F!&!\"!\"d!F!&!\"!\"o!F!&!\"!\"~!F!'!\"!&7!F!#!\"!&?!F!\"!\"!&D!F!\"!\"!"
  "&M!F!%!\"!&Y!F!%!\"!!9!G!&!\"!!A!G!&!\"!!O!G!&!\"!!Y!G!'!\"!!d!G!'!\"!\"&!G!&!\"!\"1!G!&!\"!\"B!G!&!\"!\"S!G!'!\"!\"^!G!&!\"!\"d!G!'!\"!\"o!G!&!\"!\"~!G!'!\"!&8!G!\"!\"!&>!G!#!\"!&B!G!$!\"!&L!G!#!\"!&P!G!#!\"!&X!G!#!\"!!8!H!&!\"!!A!H!&!\"!!O!H!/!\"!!c!H!'!\"!\"%!H!'!\"!\"1!H!&!\"!\"A!H!'!\"!\"S!H!'!\"!\"^!H!&!\"!\"e!H!&!\"!\"o!H!&!\"!\"~!H!'!\"!&8!H!\"!\"!&>!H!\"!\"!&B!H!\"!\"!&D!H!\"!\"!&L!H!\"!\"!&Q!H!\"!\"!&X!H!\"!\"!!8!I!&!\"!!A!I!'!\"!!O!I!.!\"!!c!I!'!\"!\"$!I!'!\"!\"1!I!.!\"!\"A!I!'!\"!\"S!I!'!\"!\"^!I!&!\"!\"f!I!&!\"!\"o!I!&!\"!\"~!I!'!\"!&8!I!#!\"!&>!I!\"!\"!&D!I!\"!\"!&L!I!\"!\"!&Q!I!\"!\"!&W!I!\"!\"!!8!J!&!\"!!B!J!&!\"!!O!J!0!\"!!c!J!'!\"!\"#!J!(!\"!\"1!J!.!\"!\"A!J!'!\"!\"S!J!'!\"!\"^!J!&!\"!\"f!J!'!\"!\"o!J!&!\"!\"~!J!'!\"!&9!J!\"!\"!&=!J!#!\"!&D!J!\"!\"!&L!J!\"!\"!&Q!J!\"!\"!&W!J!\"!\"!&Y!J!$!\"!!7!K!'!\"!!B!K!&!\"!!O!K!2!\"!!c!K!'!\"!\"\"!K!(!\"!\"1!K!.!\"!\"A!K!'!\"!\"S!K!'!\"!\"^!K!&!\"!\"g!K!&!\"!\"o!K!&!\"!\"~!K!'!\"!&9!K!\"!\"!&=!K!\"!\"!&D!K!\"!\"!&L!K!#!\"!&P!K!#!\"!&W!K!#!\"!&[!K!#!\"!!7!L!&!\"!!B!L!'!\"!!O!L!&!\"!!Y!L!(!\"!"
  "!c!L!'!\"!!~!L!)!\"!\"1!L!.!\"!\"A!L!'!\"!\"S!L!'!\"!\"^!L!&!\"!\"h!L!&!\"!\"o!L!&!\"!\"~!L!'!\"!&9!L!#!\"!&=!L!\"!\"!&D!L!\"!\"!&M!L!$!\"!&Q!L!\"!\"!&W!L!\"!\"!&\\!L!\"!\"!!7!M!2!\"!!O!M!&!\"!![!M!'!\"!!c!M!(!\"!!}!M!(!\"!\"1!M!'!\"!\"B!M!&!\"!\"S!M!'!\"!\"^!M!&!\"!\"h!M!'!\"!\"o!M!&!\"!\"~!M!'!\"!&:!M!\"!\"!&<!M!#!\"!&D!M!\"!\"!&Q!M!\"!\"!&W!M!\"!\"!&\\!M!\"!\"!!6!N!3!\"!!O!N!&!\"!![!N!'!\"!!d!N!'!\"!!|!N!(!\"!\"1!N!&!\"!\"B!N!'!\"!\"S!N!'!\"!\"^!N!&!\"!\"i!N!,!\"!\"~!N!'!\"!&:!N!\"!\"!&<!N!\"!\"!&D!N!\"!\"!&P!N!#!\"!&W!N!\"!\"!&\\!N!\"!\"!!6!O!4!\"!!O!O!&!\"!![!O!'!\"!!d!O!(!\"!!{!O!(!\"!\"1!O!&!\"!\"B!O!'!\"!\"R!O!'!\"!\"^!O!&!\"!\"i!O!,!\"!\"~!O!'!\"!&:!O!$!\"!&D!O!\"!\"!&I!O!\"!\"!&P!O!\"!\"!&T!O!\"!\"!&X!O!\"!\"!&[!O!#!\"!!5!P!5!\"!!O!P!&!\"!![!P!'!\"!!d!P!(!\"!!{!P!'!\"!\"1!P!&!\"!\"B!P!(!\"!\"Q!P!(!\"!\"^!P!&!\"!\"j!P!+!\"!\"~!P!'!\"!&;!P!\"!\"!&D!P!\"!\"!&I!P!\"!\"!&L!P!%!\"!&T!P!\"!\"!&Y!P!$!\"!!5!Q!'!\"!!D!Q!&!\"!!O!Q!&!\"!![!Q!'!\"!!e!Q!)!\"!!u!Q!\"!\"!!z!Q!'!\"!\"1!Q!&!\"!\"C!Q!(!\"!\"P!Q!(!\"!\"^!Q!&!\"!\"k!Q!*!\"!\"~!Q!'!\"!!5!R!&!\"!"
  "!D!R!'!\"!!O!R!&!\"!!Y!R!)!\"!!f!R!1!\"!!z!R!'!\"!\"1!R!&!\"!\"D!R!4!\"!\"^!R!&!\"!\"k!R!*!\"!\"~!R!'!\"!!4!S!'!\"!!D!S!'!\"!!O!S!2!\"!!f!S!1!\"!!z!S!1!\"!\"1!S!&!\"!\"E!S!2!\"!\"^!S!&!\"!\"l!S!)!\"!\"~!S!'!\"!!4!T!'!\"!!E!T!&!\"!!O!T!1!\"!!g!T!0!\"!!y!T!2!\"!\"1!T!&!\"!\"F!T!0!\"!\"^!T!&!\"!\"m!T!(!\"!\"~!T!'!\"!!4!U!'!\"!!E!U!'!\"!!O!U!0!\"!!i!U!.!\"!!y!U!2!\"!\"1!U!&!\"!\"G!U!-!\"!\"^!U!&!\"!\"m!U!(!\"!\"~!U!'!\"!!3!V!'!\"!!E!V!'!\"!!O!V!.!\"!!l!V!)!\"!!y!V!2!\"!\"1!V!&!\"!\"J!V!(!\"!\"^!V!&!\"!\"n!V!'!\"!\"~!V!'!\"!$'!g!$!\"!$)!h!\"!\"!\"~!i!\"!\"!#B!i!#!\"!#z!i!#!\"!$(!i!#!\"!$-!i!\"!\"!$:!i!$!\"!!3!j!,!\"!!D!j!'!\"!!K!j!#!\"!!V!j!#!\"!!Z!j!#!\"!!d!j!#!\"!!j!j!&!\"!!z!j!\"!\"!\"#!j!#!\"!\"-!j!'!\"!\"=!j!'!\"!\"d!j!\"!\"!\"m!j!#!\"!\"~!j!\"!\"!#B!j!#!\"!#c!j!#!\"!#z!j!#!\"!$$!j!#!\"!$(!j!\"!\"!$-!j!\"!\"!$9!j!#!\"!$<!j!\"!\"!!4!k!*!\"!!B!k!)!\"!!L!k!#!\"!!U!k!#!\"!!Z!k!$!\"!!d!k!#!\"!!j!k!#!\"!!m!k!$!\"!!y!k!#!\"!\"\"!k!$!\"!\"-!k!)!\"!\";!k!)!\"!\"d!k!\"!\"!\"m!k!#!\"!#B!k!#!\"!#d!k!#!\"!#w!k!'!\"!$$!k!$!\"!$8!k!#!\"!$X!k!\"!\"!"
  "!8!l!\"!\"!!A!l!#!\"!!L!l!#!\"!!U!l!#!\"!!Z!l!%!\"!!d!l!#!\"!!o!l!#!\"!!y!l!\"!\"!\"\"!l!\"!\"!\"$!l!#!\"!\"-!l!#!\"!\"3!l!#!\"!\":!l!#!\"!\"d!l!\"!\"!\"m!l!#!\"!#B!l!#!\"!#e!l!#!\"!#z!l!#!\"!$#!l!\"!\"!$'!l!\"!\"!$8!l!#!\"!$W!l!#!\"!!8!m!\"!\"!!@!m!#!\"!!L!m!#!\"!!U!m!#!\"!!Z!m!%!\"!!d!m!#!\"!!o!m!#!\"!!x!m!#!\"!\"\"!m!\"!\"!\"$!m!#!\"!\"-!m!#!\"!\"3!m!#!\"!\"9!m!#!\"!\"d!m!\"!\"!\"m!m!#!\"!#B!m!#!\"!#z!m!#!\"!$8!m!#!\"!$W!m!#!\"!!8!n!\"!\"!!@!n!\"!\"!!M!n!#!\"!!T!n!#!\"!!Z!n!#!\"!!]!n!#!\"!!d!n!#!\"!!o!n!#!\"!!x!n!\"!\"!\"!!n!#!\"!\"%!n!\"!\"!\"-!n!#!\"!\"3!n!#!\"!\"9!n!\"!\"!\"d!n!\"!\"!\"m!n!#!\"!\"r!n!\"!\"!\"u!n!%!\"!\"~!n!\"!\"!#'!n!&!\"!#1!n!&!\"!#=!n!%!\"!#B!n!#!\"!#I!n!%!\"!#V!n!#!\"!#^!n!#!\"!#c!n!&!\"!#u!n!%!\"!#z!n!#!\"!$#!n!&!\"!$-!n!\"!\"!$7!n!&!\"!$A!n!&!\"!$K!n!\"!\"!$N!n!%!\"!$U!n!(!\"!!8!o!\"!\"!!?!o!#!\"!!M!o!#!\"!!T!o!#!\"!!Z!o!#!\"!!^!o!#!\"!!d!o!#!\"!!o!o!#!\"!!w!o!#!\"!\"!!o!\"!\"!\"%!o!#!\"!\"-!o!#!\"!\"2!o!#!\"!\"8!o!#!\"!\"d!o!\"!\"!\"m!o!#!\"!\"r!o!%!\"!\"w!o!$!\"!\"~!o!\"!\"!#&!o!$!\"!#*!o!#!\"!#/!o!%!\"!#4!o!$!\"!"
  "#;!o!$!\"!#?!o!&!\"!#G!o!$!\"!#K!o!$!\"!#W!o!\"!\"!#^!o!\"!\"!#b!o!$!\"!#f!o!$!\"!#s!o!$!\"!#w!o!&!\"!$!!o!%!\"!$&!o!$!\"!$-!o!\"!\"!$8!o!#!\"!$?!o!%!\"!$D!o!$!\"!$K!o!%!\"!$P!o!$!\"!$W!o!#!\"!!8!p!\"!\"!!?!p!#!\"!!M!p!#!\"!!T!p!#!\"!!Z!p!#!\"!!^!p!#!\"!!d!p!#!\"!!n!p!#!\"!!w!p!#!\"!!~!p!#!\"!\"%!p!#!\"!\"-!p!'!\"!\"8!p!#!\"!\"W!p!#!\"!\"d!p!\"!\"!\"m!p!#!\"!\"r!p!#!\"!\"x!p!#!\"!\"~!p!\"!\"!#%!p!#!\"!#/!p!#!\"!#6!p!#!\"!#;!p!#!\"!#A!p!$!\"!#G!p!\"!\"!#M!p!#!\"!#W!p!#!\"!#]!p!#!\"!#b!p!\"!\"!#g!p!#!\"!#s!p!#!\"!#y!p!$!\"!$!!p!#!\"!$(!p!#!\"!$-!p!\"!\"!$8!p!#!\"!$?!p!#!\"!$F!p!#!\"!$K!p!#!\"!$Q!p!#!\"!$W!p!#!\"!!8!q!\"!\"!!?!q!#!\"!!N!q!#!\"!!S!q!#!\"!!Z!q!#!\"!!_!q!#!\"!!d!q!#!\"!!k!q!%!\"!!w!q!\"!\"!!~!q!#!\"!\"&!q!\"!\"!\"-!q!)!\"!\"8!q!#!\"!\"X!q!#!\"!\"d!q!\"!\"!\"m!q!#!\"!\"r!q!#!\"!\"y!q!\"!\"!\"~!q!\"!\"!#$!q!#!\"!#.!q!#!\"!#6!q!#!\"!#:!q!#!\"!#B!q!#!\"!#F!q!#!\"!#M!q!#!\"!#W!q!#!\"!#]!q!#!\"!#g!q!#!\"!#r!q!#!\"!#z!q!#!\"!#~!q!#!\"!$(!q!#!\"!$-!q!\"!\"!$8!q!#!\"!$>!q!#!\"!$F!q!#!\"!$K!q!#!\"!$R!q!\"!\"!$W!q!#!\"!"
  "!8!r!\"!\"!!?!r!#!\"!!N!r!#!\"!!S!r!#!\"!!Z!r!#!\"!!`!r!\"!\"!!d!r!#!\"!!m!r!$!\"!!v!r!#!\"!!~!r!\"!\"!\"&!r!#!\"!\"-!r!#!\"!\"3!r!#!\"!\"8!r!#!\"!\"L!r!/!\"!\"d!r!\"!\"!\"m!r!#!\"!\"r!r!\"!\"!\"y!r!\"!\"!\"~!r!\"!\"!#$!r!#!\"!#.!r!#!\"!#6!r!#!\"!#:!r!#!\"!#B!r!#!\"!#F!r!*!\"!#X!r!\"!\"!#]!r!\"!\"!#d!r!&!\"!#r!r!#!\"!#z!r!#!\"!#~!r!#!\"!$(!r!#!\"!$-!r!\"!\"!$8!r!#!\"!$>!r!#!\"!$F!r!#!\"!$K!r!\"!\"!$R!r!\"!\"!$W!r!#!\"!!8!s!\"!\"!!?!s!#!\"!!O!s!\"!\"!!S!s!\"!\"!!Z!s!#!\"!!`!s!#!\"!!d!s!#!\"!!o!s!#!\"!!v!s!\"!\"!!}!s!*!\"!\"-!s!#!\"!\"4!s!#!\"!\"8!s!#!\"!\"X!s!#!\"!\"d!s!#!\"!\"m!s!#!\"!\"r!s!\"!\"!\"y!s!\"!\"!\"~!s!\"!\"!#$!s!#!\"!#.!s!#!\"!#7!s!\"!\"!#:!s!#!\"!#B!s!#!\"!#F!s!#!\"!#X!s!#!\"!#\\!s!#!\"!#b!s!%!\"!#g!s!#!\"!#r!s!#!\"!#z!s!#!\"!#~!s!#!\"!$)!s!\"!\"!$-!s!\"!\"!$8!s!#!\"!$>!s!#!\"!$G!s!\"!\"!$K!s!\"!\"!$R!s!\"!\"!$W!s!#!\"!!8!t!\"!\"!!?!t!#!\"!!O!t!#!\"!!R!t!#!\"!!Z!t!#!\"!!a!t!#!\"!!d!t!#!\"!!p!t!#!\"!!u!t!#!\"!!}!t!+!\"!\"-!t!#!\"!\"4!t!#!\"!\"8!t!#!\"!\"W!t!#!\"!\"d!t!#!\"!\"m!t!#!\"!\"r!t!\"!\"!\"y!t!\"!\"!\"~!t!\"!\"!#$!t!#!\"!"
  "#.!t!#!\"!#6!t!#!\"!#:!t!#!\"!#B!t!#!\"!#F!t!#!\"!#X!t!#!\"!#\\!t!\"!\"!#a!t!#!\"!#h!t!\"!\"!#r!t!#!\"!#z!t!#!\"!#~!t!#!\"!$(!t!#!\"!$-!t!\"!\"!$8!t!#!\"!$>!t!#!\"!$F!t!#!\"!$K!t!\"!\"!$R!t!\"!\"!$W!t!#!\"!!8!u!\"!\"!!@!u!#!\"!!O!u!#!\"!!R!u!#!\"!!Z!u!#!\"!!a!u!#!\"!!d!u!#!\"!!p!u!#!\"!!u!u!#!\"!!}!u!\"!\"!\"'!u!#!\"!\"-!u!#!\"!\"4!u!#!\"!\"9!u!#!\"!\"d!u!#!\"!\"m!u!\"!\"!\"r!u!\"!\"!\"y!u!\"!\"!\"~!u!\"!\"!#$!u!#!\"!#.!u!#!\"!#6!u!#!\"!#:!u!#!\"!#B!u!#!\"!#F!u!#!\"!#Y!u!\"!\"!#\\!u!\"!\"!#a!u!#!\"!#g!u!#!\"!#r!u!#!\"!#z!u!#!\"!#~!u!#!\"!$(!u!#!\"!$-!u!\"!\"!$8!u!#!\"!$>!u!#!\"!$F!u!#!\"!$K!u!\"!\"!$R!u!\"!\"!$W!u!#!\"!!8!v!\"!\"!!@!v!$!\"!!I!v!\"!\"!!P!v!$!\"!!Z!v!#!\"!!b!v!%!\"!!o!v!#!\"!!u!v!\"!\"!!|!v!#!\"!\"(!v!\"!\"!\"-!v!#!\"!\"3!v!$!\"!\"9!v!$!\"!\"B!v!\"!\"!\"d!v!$!\"!\"l!v!#!\"!\"r!v!\"!\"!\"y!v!\"!\"!\"~!v!\"!\"!#%!v!#!\"!#/!v!#!\"!#6!v!#!\"!#;!v!#!\"!#A!v!$!\"!#G!v!#!\"!#Y!v!%!\"!#a!v!#!\"!#g!v!#!\"!#s!v!#!\"!#y!v!$!\"!$!!v!#!\"!$(!v!#!\"!$-!v!\"!\"!$8!v!#!\"!$?!v!#!\"!$F!v!#!\"!$K!v!\"!\"!$R!v!\"!\"!$W!v!#!\"!!8!w!\"!\"!"
  "!A!w!*!\"!!P!w!$!\"!!Z!w!#!\"!!c!w!$!\"!!i!w!$!\"!!m!w!%!\"!!t!w!#!\"!!|!w!#!\"!\"(!w!#!\"!\"-!w!)!\"!\":!w!*!\"!\"e!w!)!\"!\"r!w!\"!\"!\"y!w!\"!\"!\"~!w!\"!\"!#%!w!%!\"!#*!w!#!\"!#/!w!%!\"!#4!w!$!\"!#;!w!$!\"!#?!w!&!\"!#G!w!$!\"!#L!w!#!\"!#Z!w!#!\"!#a!w!$!\"!#e!w!%!\"!#s!w!$!\"!#w!w!&!\"!$!!w!%!\"!$&!w!$!\"!$-!w!\"!\"!$8!w!#!\"!$?!w!%!\"!$D!w!$!\"!$K!w!\"!\"!$R!w!\"!\"!$X!w!#!\"!!8!x!\"!\"!!C!x!'!\"!!P!x!$!\"!!Z!x!#!\"!!c!x!$!\"!!j!x!&!\"!!t!x!\"!\"!!{!x!#!\"!\"(!x!#!\"!\"-!x!'!\"!\"<!x!'!\"!\"g!x!&!\"!\"r!x!\"!\"!\"y!x!\"!\"!\"~!x!\"!\"!#'!x!%!\"!#1!x!%!\"!#<!x!%!\"!#B!x!#!\"!#I!x!%!\"!#Z!x!#!\"!#b!x!%!\"!#h!x!\"!\"!#t!x!%!\"!#z!x!#!\"!$#!x!%!\"!$-!x!\"!\"!$8!x!#!\"!$A!x!%!\"!$K!x!\"!\"!$R!x!\"!\"!$X!x!$!\"!!s!y!#!\"!!s!z!\"!\"!!_\"E!$!\"!\"5\"E!$!\"!\"M\"E!$!\"!\"W\"E!%!\"!$i\"E!$!\"!$}\"E!$!\"!!C\"F!*!\"!!_\"F!$!\"!\"5\"F!$!\"!\"L\"F!%!\"!\"V\"F!&!\"!$R\"F!&!\"!$h\"F!%!\"!$}\"F!$!\"!!C\"G!,!\"!!_\"G!$!\"!\"5\"G!$!\"!\"L\"G!%!\"!\"U\"G!'!\"!\"`\"G!\"!\"!$R\"G!&!\"!$h\"G!%!\"!$}\"G!$!\"!!C\"H!-!\"!!_\"H!$!\"!\"5\"H!$!\"!\"M\"H!$!\"!\"U\"H!%!\"!\"]\"H!%!\"!"
  "$Q\"H!(!\"!$i\"H!$!\"!$}\"H!$!\"!!C\"I!%!\"!!J\"I!&!\"!!_\"I!$!\"!\"5\"I!$!\"!\"U\"I!$!\"!\"]\"I!%!\"!$Q\"I!(!\"!$}\"I!$!\"!!C\"J!%!\"!!K\"J!%!\"!!_\"J!$!\"!\"5\"J!$!\"!\"U\"J!$!\"!\"]\"J!%!\"!$Q\"J!$!\"!$U\"J!$!\"!$}\"J!$!\"!!C\"K!%!\"!!K\"K!%!\"!!S\"K!'!\"!!_\"K!$!\"!!d\"K!%!\"!!m\"K!$!\"!!r\"K!%!\"!!}\"K!'!\"!\",\"K!&!\"!\"5\"K!$!\"!\":\"K!%!\"!\"C\"K!$!\"!\"H\"K!#!\"!\"M\"K!$!\"!\"S\"K!2!\"!$P\"K!%!\"!$U\"K!%!\"!$_\"K!$!\"!$d\"K!#!\"!$i\"K!$!\"!$q\"K!'!\"!$}\"K!$!\"!!C\"L!%!\"!!J\"L!%!\"!!R\"L!)!\"!!_\"L!$!\"!!c\"L!'!\"!!m\"L!$!\"!!q\"L!'!\"!!|\"L!(!\"!\"*\"L!)!\"!\"5\"L!$!\"!\"9\"L!'!\"!\"C\"L!(!\"!\"M\"L!$!\"!\"S\"L!2!\"!$P\"L!$!\"!$U\"L!%!\"!$_\"L!(!\"!$i\"L!$!\"!$p\"L!)!\"!$}\"L!$!\"!!C\"M!+!\"!!R\"M!*!\"!!_\"M!,!\"!!m\"M!,!\"!!{\"M!)!\"!\")\"M!*!\"!\"5\"M!,!\"!\"C\"M!(!\"!\"M\"M!$!\"!\"S\"M!2!\"!$P\"M!$!\"!$V\"M!$!\"!$_\"M!(!\"!$i\"M!$!\"!$p\"M!*!\"!$}\"M!$!\"!!C\"N!+!\"!!R\"N!\"!\"!!X\"N!$!\"!!_\"N!%!\"!!f\"N!%!\"!!m\"N!%!\"!!t\"N!%!\"!!{\"N!%!\"!\"$\"N!\"!\"!\"(\"N!&!\"!\"1\"N!\"!\"!\"5\"N!%!\"!\"<\"N!%!\"!\"C\"N!%!\"!\"M\"N!$!\"!\"U\"N!$!\"!\"]\"N!%!\"!$O\"N!%!\"!$V\"N!%!\"!$_\"N!%!\"!$i\"N!$!\"!$p\"N!\"!\"!"
  "$v\"N!$!\"!$}\"N!$!\"!!C\"O!-!\"!!U\"O!(!\"!!_\"O!$!\"!!f\"O!%!\"!!m\"O!$!\"!!t\"O!%!\"!!{\"O!&!\"!\"(\"O!%!\"!\"5\"O!$!\"!\"<\"O!%!\"!\"C\"O!%!\"!\"M\"O!$!\"!\"U\"O!$!\"!\"]\"O!%!\"!$O\"O!$!\"!$V\"O!%!\"!$_\"O!%!\"!$i\"O!$!\"!$s\"O!(!\"!$}\"O!$!\"!!C\"P!%!\"!!K\"P!%!\"!!R\"P!+!\"!!_\"P!$!\"!!f\"P!%!\"!!m\"P!$!\"!!t\"P!%!\"!!{\"P!(!\"!\"(\"P!%!\"!\"5\"P!$!\"!\"<\"P!%!\"!\"C\"P!$!\"!\"M\"P!$!\"!\"U\"P!$!\"!\"]\"P!%!\"!$N\"P!-!\"!$_\"P!$!\"!$i\"P!$!\"!$p\"P!+!\"!$}\"P!$!\"!!C\"Q!%!\"!!K\"Q!%!\"!!Q\"Q!%!\"!!X\"Q!%!\"!!_\"Q!$!\"!!f\"Q!%!\"!!m\"Q!$!\"!!t\"Q!%!\"!!}\"Q!'!\"!\"(\"Q!%!\"!\"5\"Q!$!\"!\"<\"Q!%!\"!\"C\"Q!$!\"!\"M\"Q!$!\"!\"U\"Q!$!\"!\"]\"Q!%!\"!$N\"Q!.!\"!$_\"Q!$!\"!$i\"Q!$!\"!$o\"Q!%!\"!$v\"Q!%!\"!$}\"Q!$!\"!!C\"R!%!\"!!K\"R!&!\"!!Q\"R!$!\"!!X\"R!%!\"!!_\"R!$!\"!!f\"R!%!\"!!m\"R!$!\"!!t\"R!%!\"!\"!\"R!&!\"!\"(\"R!%!\"!\"5\"R!$!\"!\"<\"R!%!\"!\"C\"R!$!\"!\"M\"R!$!\"!\"U\"R!$!\"!\"]\"R!%!\"!$N\"R!.!\"!$_\"R!$!\"!$i\"R!$!\"!$o\"R!$!\"!$v\"R!%!\"!$}\"R!$!\"!!C\"S!%!\"!!K\"S!%!\"!!Q\"S!$!\"!!W\"S!&!\"!!_\"S!$!\"!!f\"S!%!\"!!m\"S!$!\"!!t\"S!%!\"!!{\"S!\"!\"!\"\"\"S!%!\"!\"(\"S!&!\"!\"1\"S!\"!\"!\"5\"S!$!\"!\"<\"S!%!\"!"
  "\"C\"S!$!\"!\"M\"S!$!\"!\"U\"S!$!\"!\"]\"S!&!\"!$M\"S!%!\"!$X\"S!$!\"!$_\"S!$!\"!$i\"S!$!\"!$o\"S!$!\"!$u\"S!&!\"!$}\"S!$!\"!!C\"T!-!\"!!Q\"T!,!\"!!_\"T!$!\"!!f\"T!%!\"!!m\"T!$!\"!!t\"T!%!\"!!{\"T!*!\"!\")\"T!*!\"!\"5\"T!$!\"!\"<\"T!%!\"!\"C\"T!$!\"!\"M\"T!$!\"!\"U\"T!$!\"!\"^\"T!'!\"!$M\"T!%!\"!$X\"T!%!\"!$_\"T!$!\"!$i\"T!$!\"!$o\"T!,!\"!$}\"T!$!\"!!C\"U!,!\"!!Q\"U!'!\"!!X\"U!%!\"!!_\"U!$!\"!!f\"U!%!\"!!m\"U!$!\"!!t\"U!%!\"!!{\"U!)!\"!\"*\"U!)!\"!\"5\"U!$!\"!\"<\"U!%!\"!\"C\"U!$!\"!\"M\"U!$!\"!\"U\"U!$!\"!\"^\"U!'!\"!$M\"U!%!\"!$X\"U!%!\"!$_\"U!$!\"!$i\"U!$!\"!$o\"U!'!\"!$v\"U!%!\"!$}\"U!$!\"!!C\"V!*!\"!!R\"V!%!\"!!X\"V!%!\"!!_\"V!$!\"!!f\"V!%!\"!!m\"V!$!\"!!t\"V!%!\"!!{\"V!'!\"!\"+\"V!'!\"!\"5\"V!$!\"!\"<\"V!%!\"!\"C\"V!$!\"!\"M\"V!$!\"!\"U\"V!$!\"!\"_\"V!&!\"!$L\"V!%!\"!$X\"V!&!\"!$_\"V!$!\"!$i\"V!$!\"!$p\"V!%!\"!$v\"V!%!\"!$}\"V!$!\"!!E\"j!&!\"!!P\"j!)!\"!!`\"j!'!\"!!l\"j!&!\"!!w\"j!)!\"!\"(\"j!&!\"!\"2\"j!$!\"!\":\"j!$!\"!\"?\"j!%!\"!\"H\"j!#!\"!\"\\\"j!'!\"!\"g\"j!*!\"!\"t\"j!$!\"!$P\"j!&!\"!$[\"j!)!\"!$k\"j!'!\"!$w\"j!&!\"!%&\"j!&!\"!%1\"j!(!\"!%>\"j!*!\"!%M\"j!&!\"!%X\"j!$!\"!%t\"j!'!\"!&!\"j!*!\"!&.\"j!$!\"!"
  "!E\"k!&!\"!!P\"k!*!\"!!^\"k!*!\"!!j\"k!)!\"!!w\"k!*!\"!\"(\"k!&!\"!\"2\"k!$!\"!\":\"k!$!\"!\"?\"k!%!\"!\"H\"k!#!\"!\"Z\"k!*!\"!\"g\"k!*!\"!\"t\"k!$!\"!$P\"k!&!\"!$[\"k!*!\"!$i\"k!*!\"!$u\"k!)!\"!%&\"k!&!\"!%1\"k!*!\"!%>\"k!*!\"!%M\"k!&!\"!%X\"k!$!\"!%r\"k!*!\"!&!\"k!*!\"!&.\"k!$!\"!!E\"l!&!\"!!P\"l!+!\"!!^\"l!*!\"!!j\"l!*!\"!!w\"l!+!\"!\"(\"l!&!\"!\"2\"l!$!\"!\":\"l!$!\"!\"?\"l!&!\"!\"H\"l!#!\"!\"Z\"l!*!\"!\"g\"l!*!\"!\"t\"l!$!\"!$P\"l!&!\"!$[\"l!+!\"!$i\"l!*!\"!$u\"l!*!\"!%&\"l!&!\"!%1\"l!*!\"!%>\"l!*!\"!%M\"l!&!\"!%X\"l!$!\"!%r\"l!*!\"!&!\"l!*!\"!&.\"l!$!\"!!E\"m!#!\"!!H\"m!$!\"!!P\"m!#!\"!!V\"m!%!\"!!]\"m!%!\"!!f\"m!\"!\"!!k\"m!\"!\"!!p\"m!$!\"!!w\"m!#!\"!!}\"m!%!\"!\"(\"m!#!\"!\"+\"m!$!\"!\"2\"m!$!\"!\":\"m!$!\"!\"?\"m!&!\"!\"H\"m!#!\"!\"Y\"m!%!\"!\"b\"m!\"!\"!\"g\"m!$!\"!\"t\"m!$!\"!$P\"m!#!\"!$S\"m!$!\"!$[\"m!#!\"!$a\"m!%!\"!$h\"m!%!\"!$q\"m!\"!\"!$v\"m!\"!\"!${\"m!$!\"!%&\"m!#!\"!%)\"m!$!\"!%1\"m!#!\"!%7\"m!$!\"!%A\"m!$!\"!%M\"m!#!\"!%P\"m!$!\"!%X\"m!$!\"!%q\"m!%!\"!%z\"m!\"!\"!&!\"m!$!\"!&.\"m!$!\"!!D\"n!$!\"!!H\"n!$!\"!!P\"n!#!\"!!W\"n!$!\"!!\\\"n!%!\"!!p\"n!$!\"!!w\"n!#!\"!!~\"n!$!\"!\"'\"n!$!\"!\"+\"n!$!\"!"
  "\"2\"n!$!\"!\":\"n!$!\"!\"?\"n!'!\"!\"H\"n!#!\"!\"Y\"n!%!\"!\"g\"n!$!\"!\"t\"n!$!\"!$O\"n!$!\"!$S\"n!$!\"!$[\"n!#!\"!$b\"n!$!\"!$g\"n!%!\"!${\"n!$!\"!%%\"n!$!\"!%)\"n!$!\"!%1\"n!#!\"!%7\"n!%!\"!%A\"n!$!\"!%L\"n!$!\"!%P\"n!$!\"!%X\"n!$!\"!%q\"n!%!\"!&!\"n!$!\"!&.\"n!$!\"!!D\"o!#!\"!!H\"o!$!\"!!P\"o!#!\"!!V\"o!$!\"!!\\\"o!$!\"!!p\"o!$!\"!!w\"o!#!\"!!}\"o!$!\"!\"'\"o!#!\"!\"+\"o!$!\"!\"2\"o!,!\"!\"?\"o!'!\"!\"H\"o!#!\"!\"Z\"o!%!\"!\"g\"o!$!\"!\"t\"o!$!\"!$O\"o!#!\"!$S\"o!$!\"!$[\"o!#!\"!$a\"o!$!\"!$g\"o!$!\"!${\"o!$!\"!%%\"o!#!\"!%)\"o!$!\"!%1\"o!#!\"!%7\"o!$!\"!%A\"o!$!\"!%L\"o!#!\"!%P\"o!$!\"!%X\"o!$!\"!%r\"o!%!\"!&!\"o!$!\"!&.\"o!$!\"!!D\"p!#!\"!!I\"p!$!\"!!P\"p!)!\"!!\\\"p!$!\"!!p\"p!$!\"!!w\"p!)!\"!\"'\"p!#!\"!\",\"p!$!\"!\"2\"p!,!\"!\"?\"p!$!\"!\"C\"p!$!\"!\"H\"p!#!\"!\"Z\"p!(!\"!\"g\"p!*!\"!\"t\"p!$!\"!$O\"p!#!\"!$T\"p!$!\"!$[\"p!)!\"!$g\"p!$!\"!${\"p!$!\"!%%\"p!#!\"!%*\"p!$!\"!%1\"p!#!\"!%7\"p!$!\"!%A\"p!$!\"!%L\"p!#!\"!%Q\"p!$!\"!%X\"p!$!\"!%r\"p!(!\"!&!\"p!*!\"!&.\"p!$!\"!!C\"q!$!\"!!I\"q!$!\"!!P\"q!)!\"!!\\\"q!$!\"!!o\"q!$!\"!!w\"q!)!\"!\"&\"q!$!\"!\",\"q!$!\"!\"2\"q!,!\"!\"?\"q!$!\"!\"C\"q!$!\"!\"H\"q!#!\"!"
  "\"[\"q!(!\"!\"g\"q!*!\"!\"t\"q!$!\"!$N\"q!$!\"!$T\"q!$!\"!$[\"q!)!\"!$g\"q!$!\"!$z\"q!$!\"!%$\"q!$!\"!%*\"q!$!\"!%1\"q!)!\"!%A\"q!$!\"!%K\"q!$!\"!%Q\"q!$!\"!%X\"q!$!\"!%s\"q!(!\"!&!\"q!*!\"!&.\"q!$!\"!!C\"r!$!\"!!I\"r!$!\"!!P\"r!+!\"!!\\\"r!$!\"!!n\"r!%!\"!!w\"r!+!\"!\"&\"r!$!\"!\",\"r!$!\"!\"2\"r!$!\"!\":\"r!$!\"!\"?\"r!$!\"!\"D\"r!$!\"!\"H\"r!#!\"!\"^\"r!&!\"!\"g\"r!*!\"!\"t\"r!$!\"!$N\"r!$!\"!$T\"r!$!\"!$[\"r!+!\"!$g\"r!$!\"!$y\"r!%!\"!%$\"r!$!\"!%*\"r!$!\"!%1\"r!(!\"!%A\"r!$!\"!%K\"r!$!\"!%Q\"r!$!\"!%X\"r!$!\"!%v\"r!&!\"!&!\"r!*!\"!&.\"r!$!\"!!C\"s!*!\"!!P\"s!#!\"!!W\"s!$!\"!!\\\"s!$!\"!!m\"s!%!\"!!w\"s!#!\"!!~\"s!$!\"!\"&\"s!*!\"!\"2\"s!$!\"!\":\"s!$!\"!\"?\"s!$!\"!\"D\"s!'!\"!\"`\"s!%!\"!\"g\"s!$!\"!\"t\"s!$!\"!$N\"s!*!\"!$[\"s!#!\"!$b\"s!$!\"!$g\"s!$!\"!$x\"s!%!\"!%$\"s!*!\"!%1\"s!(!\"!%A\"s!$!\"!%K\"s!*!\"!%X\"s!$!\"!%x\"s!%!\"!&!\"s!$!\"!&.\"s!$!\"!!B\"t!,!\"!!P\"t!#!\"!!W\"t!$!\"!!\\\"t!%!\"!!l\"t!%!\"!!w\"t!#!\"!!~\"t!$!\"!\"%\"t!,!\"!\"2\"t!$!\"!\":\"t!$!\"!\"?\"t!$!\"!\"E\"t!&!\"!\"a\"t!$!\"!\"g\"t!$!\"!\"t\"t!$!\"!$M\"t!,!\"!$[\"t!#!\"!$b\"t!$!\"!$g\"t!%!\"!$w\"t!%!\"!%#\"t!,!\"!%1\"t!#!\"!%5\"t!%!\"!"
  "%A\"t!$!\"!%J\"t!,!\"!%X\"t!$!\"!%y\"t!$!\"!&!\"t!$!\"!&.\"t!$!\"!!B\"u!,!\"!!P\"u!#!\"!!V\"u!%!\"!!]\"u!%!\"!!f\"u!\"!\"!!k\"u!$!\"!!w\"u!#!\"!!}\"u!%!\"!\"%\"u!,!\"!\"2\"u!$!\"!\":\"u!$!\"!\"?\"u!$!\"!\"E\"u!&!\"!\"Y\"u!#!\"!\"`\"u!%!\"!\"g\"u!$!\"!\"t\"u!$!\"!$M\"u!,!\"!$[\"u!#!\"!$a\"u!%!\"!$h\"u!%!\"!$q\"u!\"!\"!$v\"u!$!\"!%#\"u!,!\"!%1\"u!#!\"!%6\"u!%!\"!%A\"u!$!\"!%J\"u!,!\"!%X\"u!$!\"!%q\"u!#!\"!%x\"u!%!\"!&!\"u!$!\"!&.\"u!$!\"!!B\"v!$!\"!!J\"v!$!\"!!P\"v!+!\"!!]\"v!+!\"!!j\"v!+!\"!!w\"v!+!\"!\"%\"v!$!\"!\"-\"v!$!\"!\"2\"v!$!\"!\":\"v!$!\"!\"?\"v!$!\"!\"F\"v!%!\"!\"Y\"v!+!\"!\"g\"v!*!\"!\"t\"v!+!\"!$M\"v!$!\"!$U\"v!$!\"!$[\"v!+!\"!$h\"v!+!\"!$u\"v!+!\"!%#\"v!$!\"!%+\"v!$!\"!%1\"v!#!\"!%7\"v!$!\"!%>\"v!*!\"!%J\"v!$!\"!%R\"v!$!\"!%X\"v!+!\"!%q\"v!+!\"!&!\"v!*!\"!&.\"v!+!\"!!A\"w!$!\"!!K\"w!$!\"!!P\"w!*!\"!!^\"w!*!\"!!j\"w!+!\"!!w\"w!*!\"!\"$\"w!$!\"!\".\"w!$!\"!\"2\"w!$!\"!\":\"w!$!\"!\"?\"w!$!\"!\"F\"w!%!\"!\"Y\"w!+!\"!\"g\"w!*!\"!\"t\"w!+!\"!$L\"w!$!\"!$V\"w!$!\"!$[\"w!*!\"!$i\"w!*!\"!$u\"w!+!\"!%\"\"w!$!\"!%,\"w!$!\"!%1\"w!#!\"!%7\"w!%!\"!%>\"w!*!\"!%I\"w!$!\"!%S\"w!$!\"!%X\"w!+!\"!%q\"w!+!\"!&!\"w!*!\"!"
  "&.\"w!+!\"!!A\"x!$!\"!!K\"x!$!\"!!P\"x!(!\"!!`\"x!&!\"!!j\"x!+!\"!!w\"x!(!\"!\"$\"x!$!\"!\".\"x!$!\"!\"2\"x!$!\"!\":\"x!$!\"!\"?\"x!$!\"!\"F\"x!%!\"!\"Z\"x!(!\"!\"g\"x!*!\"!\"t\"x!+!\"!$L\"x!$!\"!$V\"x!$!\"!$[\"x!(!\"!$k\"x!&!\"!$u\"x!+!\"!%\"\"x!$!\"!%,\"x!$!\"!%1\"x!#!\"!%8\"x!$!\"!%>\"x!*!\"!%I\"x!$!\"!%S\"x!$!\"!%X\"x!+!\"!%r\"x!(!\"!&!\"x!*!\"!&.\"x!+!\"!\"K\"{!.!\"!%c\"{!.!\"!\"K\"|!.!\"!%c\"|!.!\"!\"K\"}!.!\"!%c\"}!.!\"!!H#I!#!\"!$?#I!#!\"!!H#J!\"!\"!\"-#J!$!\"!\"W#J!\"!\"!#3#J!#!\"!#b#J!\"!\"!#p#J!$!\"!$@#J!\"!\"!$L#J!\"!\"!$u#J!\"!\"!%3#J!\"!\"!!5#K!'!\"!!D#K!#!\"!!G#K!\"!\"!!u#K!\"!\"!\",#K!#!\"!\"/#K!\"!\"!\"W#K!\"!\"!\"p#K!\"!\"!\"t#K!\"!\"!#3#K!#!\"!#=#K!\"!\"!#b#K!\"!\"!#r#K!\"!\"!$@#K!#!\"!$C#K!#!\"!$L#K!\"!\"!$d#K!%!\"!$i#K!\"!\"!$u#K!\"!\"!$}#K!#!\"!%3#K!\"!\"!!5#L!)!\"!!C#L!\"!\"!!E#L!\"!\"!!t#L!\"!\"!!}#L!\"!\"!\"+#L!#!\"!\"K#L!\"!\"!\"W#L!\"!\"!\"p#L!\"!\"!\"s#L!#!\"!#0#L!'!\"!#<#L!\"!\"!#b#L!\"!\"!#q#L!#!\"!$B#L!%!\"!$d#L!\"!\"!$g#L!$!\"!$u#L!\"!\"!$|#L!\"!\"!$~#L!\"!\"!%3#L!\"!\"!!5#M!#!\"!!;#M!#!\"!!B#M!\"!\"!!F#M!\"!\"!!s#M!\"!\"!!|#M!#!\"!\"+#M!#!\"!\"J#M!#!\"!"
  "\"W#M!\"!\"!\"q#M!$!\"!#3#M!#!\"!#;#M!\"!\"!#b#M!\"!\"!#q#M!\"!\"!$B#M!\"!\"!$F#M!\"!\"!$u#M!\"!\"!${#M!\"!\"!%!#M!\"!\"!%3#M!\"!\"!!5#N!#!\"!!;#N!#!\"!!|#N!#!\"!\"+#N!#!\"!\"J#N!#!\"!\"W#N!\"!\"!#3#N!#!\"!#b#N!\"!\"!$u#N!\"!\"!%3#N!\"!\"!!5#O!#!\"!!;#O!#!\"!!B#O!&!\"!!L#O!\"!\"!!O#O!$!\"!!U#O!%!\"!!d#O!\"!\"!!g#O!%!\"!!o#O!#!\"!!w#O!\"!\"!!z#O!(!\"!\"*#O!&!\"!\"4#O!&!\"!\">#O!\"!\"!\"A#O!%!\"!\"H#O!(!\"!\"W#O!\"!\"!\"Z#O!%!\"!\"e#O!&!\"!\"p#O!&!\"!\"|#O!&!\"!#.#O!%!\"!#3#O!#!\"!#:#O!&!\"!#D#O!\"!\"!#G#O!%!\"!#R#O!%!\"!#W#O!#!\"!#b#O!\"!\"!#e#O!%!\"!#o#O!&!\"!#y#O!\"!\"!#|#O!%!\"!$)#O!%!\"!$.#O!#!\"!$9#O!\"!\"!$<#O!#!\"!$B#O!&!\"!$L#O!\"!\"!$Y#O!%!\"!$^#O!#!\"!$e#O!&!\"!$u#O!\"!\"!$|#O!%!\"!%'#O!\"!\"!%*#O!%!\"!%3#O!\"!\"!%6#O!%!\"!!5#P!#!\"!!:#P!#!\"!!A#P!$!\"!!E#P!$!\"!!L#P!(!\"!!T#P!#!\"!!W#P!$!\"!!d#P!%!\"!!i#P!$!\"!!o#P!#!\"!!w#P!\"!\"!!|#P!#!\"!\"+#P!#!\"!\"2#P!%!\"!\"7#P!$!\"!\">#P!%!\"!\"C#P!$!\"!\"J#P!#!\"!\"W#P!%!\"!\"\\#P!$!\"!\"c#P!%!\"!\"h#P!$!\"!\"o#P!$!\"!\"s#P!$!\"!\"{#P!$!\"!#!#P!#!\"!#,#P!$!\"!#0#P!&!\"!#8#P!%!\"!#=#P!$!\"!"
  "#D#P!%!\"!#I#P!$!\"!#P#P!$!\"!#T#P!&!\"!#b#P!%!\"!#g#P!$!\"!#n#P!$!\"!#r#P!$!\"!#y#P!%!\"!#~#P!$!\"!$'#P!$!\"!$+#P!&!\"!$9#P!\"!\"!$;#P!$!\"!$@#P!%!\"!$E#P!$!\"!$L#P!\"!\"!$W#P!$!\"!$[#P!&!\"!$c#P!%!\"!$h#P!$!\"!$u#P!\"!\"!$z#P!$!\"!$~#P!$!\"!%'#P!%!\"!%,#P!$!\"!%3#P!%!\"!%8#P!$!\"!!5#Q!'!\"!!A#Q!\"!\"!!F#Q!#!\"!!L#Q!#!\"!!R#Q!$!\"!!X#Q!#!\"!!d#Q!#!\"!!j#Q!#!\"!!o#Q!#!\"!!w#Q!\"!\"!!|#Q!#!\"!\"+#Q!#!\"!\"2#Q!#!\"!\"9#Q!#!\"!\">#Q!#!\"!\"D#Q!#!\"!\"J#Q!#!\"!\"W#Q!#!\"!\"]#Q!#!\"!\"c#Q!#!\"!\"j#Q!#!\"!\"o#Q!\"!\"!\"t#Q!#!\"!\"z#Q!#!\"!#,#Q!#!\"!#2#Q!$!\"!#8#Q!#!\"!#?#Q!#!\"!#D#Q!#!\"!#J#Q!#!\"!#P#Q!#!\"!#V#Q!$!\"!#b#Q!#!\"!#i#Q!#!\"!#n#Q!\"!\"!#s#Q!#!\"!#y#Q!#!\"!$!#Q!#!\"!$'#Q!#!\"!$-#Q!$!\"!$9#Q!$!\"!$@#Q!#!\"!$G#Q!#!\"!$L#Q!\"!\"!$W#Q!#!\"!$]#Q!$!\"!$c#Q!#!\"!$j#Q!#!\"!$u#Q!\"!\"!$z#Q!\"!\"!%\"#Q!#!\"!%'#Q!#!\"!%-#Q!#!\"!%3#Q!#!\"!%9#Q!#!\"!!5#R!)!\"!!F#R!#!\"!!L#R!#!\"!!R#R!#!\"!!Y#R!#!\"!!d#R!#!\"!!k#R!\"!\"!!o#R!#!\"!!w#R!\"!\"!!|#R!#!\"!\"+#R!#!\"!\"1#R!#!\"!\"9#R!#!\"!\">#R!#!\"!\"E#R!\"!\"!\"J#R!#!\"!\"W#R!#!\"!"
  "\"^#R!\"!\"!\"b#R!#!\"!\"j#R!#!\"!\"t#R!#!\"!\"y#R!#!\"!#+#R!#!\"!#3#R!#!\"!#7#R!#!\"!#?#R!#!\"!#D#R!#!\"!#K#R!\"!\"!#O#R!#!\"!#W#R!#!\"!#b#R!#!\"!#i#R!#!\"!#s#R!#!\"!#y#R!#!\"!$\"#R!\"!\"!$&#R!#!\"!$.#R!#!\"!$9#R!#!\"!$?#R!#!\"!$G#R!#!\"!$L#R!\"!\"!$V#R!#!\"!$^#R!#!\"!$b#R!#!\"!$j#R!#!\"!$u#R!\"!\"!$y#R!#!\"!%\"#R!#!\"!%'#R!#!\"!%.#R!\"!\"!%3#R!#!\"!%:#R!\"!\"!!5#S!#!\"!!;#S!#!\"!!C#S!&!\"!!L#S!\"!\"!!R#S!#!\"!!Y#S!#!\"!!d#S!\"!\"!!k#S!\"!\"!!o#S!#!\"!!w#S!\"!\"!!|#S!#!\"!\"+#S!#!\"!\"1#S!#!\"!\"9#S!#!\"!\">#S!\"!\"!\"E#S!\"!\"!\"J#S!#!\"!\"W#S!\"!\"!\"^#S!\"!\"!\"b#S!#!\"!\"j#S!#!\"!\"q#S!&!\"!\"y#S!#!\"!#+#S!#!\"!#3#S!#!\"!#7#S!#!\"!#?#S!#!\"!#D#S!\"!\"!#K#S!\"!\"!#O#S!#!\"!#W#S!#!\"!#b#S!\"!\"!#j#S!\"!\"!#p#S!&!\"!#y#S!\"!\"!$\"#S!\"!\"!$&#S!#!\"!$.#S!#!\"!$9#S!#!\"!$?#S!#!\"!$G#S!#!\"!$L#S!\"!\"!$V#S!#!\"!$^#S!#!\"!$b#S!#!\"!$j#S!#!\"!$u#S!\"!\"!$y#S!*!\"!%'#S!\"!\"!%.#S!\"!\"!%3#S!\"!\"!%:#S!\"!\"!!5#T!#!\"!!<#T!#!\"!!A#T!%!\"!!F#T!#!\"!!L#T!\"!\"!!R#T!#!\"!!Y#T!#!\"!!d#T!\"!\"!!k#T!\"!\"!!o#T!#!\"!!w#T!\"!\"!!|#T!#!\"!\"+#T!#!\"!"
  "\"1#T!#!\"!\":#T!\"!\"!\">#T!\"!\"!\"E#T!\"!\"!\"J#T!#!\"!\"W#T!\"!\"!\"^#T!\"!\"!\"b#T!#!\"!\"k#T!\"!\"!\"o#T!%!\"!\"t#T!#!\"!\"y#T!#!\"!#+#T!#!\"!#3#T!#!\"!#7#T!#!\"!#@#T!\"!\"!#D#T!\"!\"!#K#T!\"!\"!#O#T!#!\"!#W#T!#!\"!#b#T!\"!\"!#j#T!\"!\"!#n#T!%!\"!#s#T!#!\"!#y#T!\"!\"!$\"#T!\"!\"!$&#T!#!\"!$.#T!#!\"!$9#T!\"!\"!$?#T!#!\"!$H#T!\"!\"!$L#T!\"!\"!$V#T!#!\"!$^#T!#!\"!$b#T!#!\"!$k#T!\"!\"!$u#T!\"!\"!$y#T!#!\"!%'#T!\"!\"!%.#T!\"!\"!%3#T!\"!\"!%:#T!\"!\"!!5#U!#!\"!!<#U!#!\"!!@#U!#!\"!!G#U!\"!\"!!L#U!\"!\"!!R#U!#!\"!!Y#U!#!\"!!d#U!\"!\"!!k#U!\"!\"!!o#U!#!\"!!v#U!#!\"!!|#U!#!\"!\"+#U!#!\"!\"1#U!#!\"!\"9#U!#!\"!\">#U!\"!\"!\"E#U!\"!\"!\"J#U!#!\"!\"W#U!\"!\"!\"^#U!\"!\"!\"b#U!#!\"!\"j#U!#!\"!\"n#U!#!\"!\"u#U!\"!\"!\"y#U!#!\"!#+#U!#!\"!#3#U!#!\"!#7#U!#!\"!#?#U!#!\"!#D#U!\"!\"!#K#U!\"!\"!#O#U!#!\"!#W#U!#!\"!#b#U!\"!\"!#i#U!#!\"!#m#U!#!\"!#t#U!\"!\"!#y#U!\"!\"!$\"#U!\"!\"!$&#U!#!\"!$.#U!#!\"!$9#U!\"!\"!$?#U!#!\"!$G#U!#!\"!$L#U!\"!\"!$V#U!#!\"!$^#U!#!\"!$b#U!#!\"!$j#U!#!\"!$u#U!\"!\"!$y#U!#!\"!%'#U!\"!\"!%.#U!\"!\"!%3#U!\"!\"!%:#U!\"!\"!!5#V!#!\"!!<#V!#!\"!!@#V!#!\"!"
  "!F#V!#!\"!!L#V!\"!\"!!R#V!#!\"!!Y#V!#!\"!!d#V!\"!\"!!k#V!\"!\"!!p#V!\"!\"!!v#V!#!\"!!|#V!#!\"!\"+#V!#!\"!\"1#V!#!\"!\"9#V!#!\"!\">#V!\"!\"!\"E#V!\"!\"!\"J#V!#!\"!\"W#V!\"!\"!\"^#V!\"!\"!\"b#V!#!\"!\"j#V!#!\"!\"n#V!#!\"!\"t#V!#!\"!\"y#V!#!\"!#+#V!#!\"!#3#V!#!\"!#7#V!#!\"!#?#V!#!\"!#D#V!\"!\"!#K#V!\"!\"!#O#V!#!\"!#W#V!#!\"!#b#V!#!\"!#i#V!#!\"!#m#V!#!\"!#s#V!#!\"!#y#V!\"!\"!$\"#V!\"!\"!$&#V!#!\"!$.#V!#!\"!$9#V!\"!\"!$?#V!#!\"!$G#V!#!\"!$L#V!\"!\"!$V#V!#!\"!$^#V!#!\"!$b#V!#!\"!$j#V!#!\"!$u#V!\"!\"!$y#V!#!\"!%'#V!\"!\"!%.#V!\"!\"!%3#V!\"!\"!%:#V!\"!\"!!5#W!#!\"!!;#W!$!\"!!@#W!#!\"!!F#W!#!\"!!L#W!\"!\"!!R#W!#!\"!!Y#W!#!\"!!d#W!\"!\"!!k#W!\"!\"!!p#W!#!\"!!v#W!#!\"!!|#W!#!\"!\"+#W!#!\"!\"2#W!#!\"!\"9#W!#!\"!\">#W!\"!\"!\"E#W!\"!\"!\"J#W!#!\"!\"W#W!\"!\"!\"^#W!\"!\"!\"c#W!#!\"!\"j#W!#!\"!\"n#W!#!\"!\"t#W!#!\"!\"z#W!#!\"!#,#W!#!\"!#2#W!$!\"!#8#W!#!\"!#?#W!#!\"!#D#W!\"!\"!#K#W!\"!\"!#P#W!#!\"!#V#W!$!\"!#b#W!#!\"!#i#W!#!\"!#m#W!#!\"!#s#W!#!\"!#y#W!\"!\"!$\"#W!\"!\"!$'#W!#!\"!$-#W!$!\"!$9#W!\"!\"!$@#W!#!\"!$G#W!#!\"!$L#W!\"!\"!$W#W!#!\"!$]#W!$!\"!$c#W!#!\"!"
  "$j#W!#!\"!$u#W!\"!\"!$z#W!#!\"!%'#W!\"!\"!%.#W!\"!\"!%3#W!\"!\"!%:#W!\"!\"!!5#X!)!\"!!@#X!$!\"!!D#X!%!\"!!L#X!\"!\"!!R#X!#!\"!!Y#X!#!\"!!d#X!\"!\"!!k#X!\"!\"!!p#X!$!\"!!t#X!%!\"!!}#X!#!\"!\"+#X!#!\"!\"2#X!%!\"!\"7#X!$!\"!\">#X!\"!\"!\"E#X!\"!\"!\"K#X!#!\"!\"W#X!\"!\"!\"^#X!\"!\"!\"c#X!%!\"!\"h#X!$!\"!\"n#X!$!\"!\"r#X!%!\"!\"z#X!%!\"!#!#X!#!\"!#,#X!$!\"!#0#X!&!\"!#8#X!%!\"!#=#X!$!\"!#D#X!\"!\"!#K#X!\"!\"!#P#X!$!\"!#T#X!&!\"!#b#X!%!\"!#g#X!$!\"!#m#X!$!\"!#q#X!%!\"!#y#X!\"!\"!$\"#X!\"!\"!$'#X!$!\"!$+#X!&!\"!$9#X!\"!\"!$@#X!%!\"!$E#X!$!\"!$L#X!\"!\"!$W#X!$!\"!$[#X!&!\"!$c#X!%!\"!$h#X!$!\"!$u#X!\"!\"!$z#X!$!\"!%!#X!#!\"!%'#X!\"!\"!%.#X!\"!\"!%3#X!\"!\"!%:#X!\"!\"!%>#X!#!\"!!5#Y!'!\"!!A#Y!%!\"!!G#Y!\"!\"!!L#Y!\"!\"!!R#Y!#!\"!!Y#Y!#!\"!!d#Y!\"!\"!!k#Y!\"!\"!!q#Y!%!\"!!w#Y!\"!\"!!}#Y!$!\"!\"+#Y!#!\"!\"4#Y!%!\"!\">#Y!\"!\"!\"E#Y!\"!\"!\"K#Y!$!\"!\"W#Y!\"!\"!\"^#Y!\"!\"!\"e#Y!%!\"!\"o#Y!%!\"!\"u#Y!\"!\"!\"|#Y!%!\"!#-#Y!%!\"!#3#Y!#!\"!#:#Y!%!\"!#D#Y!\"!\"!#K#Y!\"!\"!#Q#Y!%!\"!#W#Y!#!\"!#b#Y!\"!\"!#e#Y!%!\"!#n#Y!%!\"!#t#Y!\"!\"!#y#Y!\"!\"!$\"#Y!\"!\"!$(#Y!%!\"!"
  "$.#Y!#!\"!$9#Y!\"!\"!$B#Y!%!\"!$L#Y!\"!\"!$X#Y!%!\"!$^#Y!#!\"!$e#Y!%!\"!$u#Y!\"!\"!$|#Y!%!\"!%'#Y!\"!\"!%.#Y!\"!\"!%3#Y!\"!\"!%:#Y!\"!\"!%>#Y!#!\"!#W#Z!#!\"!$.#Z!#!\"!$^#Z!#!\"!\"r#[!\"!\"!#W#[!\"!\"!$.#[!\"!\"!$^#[!\"!\"!$}#[!#!\"!\"r#\\!\"!\"!#V#\\!#!\"!$-#\\!#!\"!$]#\\!#!\"!$}#\\!#!\"!#P#]!#!\"!#T#]!$!\"!$'#]!#!\"!$+#]!$!\"!$W#]!#!\"!$[#]!$!\"!#P#^!'!\"!$'#^!'!\"!$W#^!'!\"!\"(#j!\"!\"!\"\\#j!\"!\"!!9#k!#!\"!\"(#k!\"!\"!\"\\#k!\"!\"!\"i#k!$!\"!\"n#k!\"!\"!#0#k!(!\"!!8#l!$!\"!!P#l!\"!\"!\"(#l!\"!\"!\"\\#l!\"!\"!\"i#l!\"!\"!\"l#l!#!\"!\"q#l!\"!\"!#0#l!(!\"!#H#l!\"!\"!!8#m!\"!\"!!:#m!#!\"!!O#m!#!\"!\"(#m!\"!\"!\"\\#m!\"!\"!\"q#m!#!\"!#0#m!#!\"!#G#m!#!\"!!8#n!\"!\"!!:#n!#!\"!!O#n!#!\"!\"(#n!\"!\"!\"\\#n!\"!\"!\"q#n!\"!\"!#0#n!#!\"!#G#n!#!\"!!7#o!#!\"!!;#o!\"!\"!!B#o!#!\"!!J#o!\"!\"!!M#o!(!\"!!X#o!&!\"!!i#o!\"!\"!!~#o!&!\"!\"(#o!\"!\"!\"+#o!%!\"!\"6#o!&!\"!\"@#o!\"!\"!\"C#o!%!\"!\"T#o!&!\"!\"\\#o!\"!\"!\"_#o!%!\"!\"g#o!#!\"!\"o#o!#!\"!\"{#o!\"!\"!#0#o!#!\"!#;#o!\"!\"!#>#o!%!\"!#E#o!(!\"!#P#o!%!\"!#Y#o!\"!\"!#\\#o!#!\"!!7#p!\"!\"!!;#p!#!\"!!B#p!#!\"!!J#p!\"!\"!!O#p!#!\"!"
  "!V#p!%!\"!![#p!$!\"!!i#p!#!\"!!}#p!$!\"!\"##p!#!\"!\"(#p!%!\"!\"-#p!$!\"!\"4#p!%!\"!\"9#p!$!\"!\"@#p!%!\"!\"E#p!$!\"!\"S#p!$!\"!\"W#p!#!\"!\"\\#p!%!\"!\"a#p!$!\"!\"g#p!#!\"!\"o#p!\"!\"!\"{#p!#!\"!#0#p!#!\"!#;#p!%!\"!#@#p!$!\"!#G#p!#!\"!#N#p!$!\"!#R#p!$!\"!#Y#p!\"!\"!#[#p!$!\"!!6#q!#!\"!!;#q!#!\"!!B#q!#!\"!!J#q!\"!\"!!O#q!#!\"!!V#q!#!\"!!]#q!#!\"!!k#q!#!\"!!|#q!#!\"!\"(#q!#!\"!\".#q!#!\"!\"4#q!#!\"!\";#q!#!\"!\"@#q!#!\"!\"F#q!#!\"!\"R#q!#!\"!\"\\#q!#!\"!\"b#q!#!\"!\"g#q!#!\"!\"o#q!\"!\"!\"}#q!#!\"!#0#q!#!\"!#;#q!#!\"!#A#q!#!\"!#G#q!#!\"!#N#q!\"!\"!#T#q!#!\"!#Y#q!$!\"!!6#r!#!\"!!<#r!\"!\"!!B#r!#!\"!!J#r!\"!\"!!O#r!#!\"!!U#r!#!\"!!]#r!#!\"!!l#r!$!\"!!{#r!#!\"!\"(#r!#!\"!\"/#r!\"!\"!\"3#r!#!\"!\";#r!#!\"!\"@#r!#!\"!\"G#r!\"!\"!\"Q#r!#!\"!\"\\#r!#!\"!\"c#r!\"!\"!\"g#r!#!\"!\"o#r!\"!\"!\"~#r!$!\"!#0#r!(!\"!#;#r!#!\"!#B#r!\"!\"!#G#r!#!\"!#M#r!#!\"!#T#r!#!\"!#Y#r!#!\"!!6#s!\"!\"!!<#s!#!\"!!B#s!#!\"!!J#s!\"!\"!!O#s!#!\"!!U#s!#!\"!!]#s!#!\"!!n#s!$!\"!!{#s!#!\"!\"(#s!\"!\"!\"/#s!\"!\"!\"3#s!#!\"!\";#s!#!\"!\"@#s!\"!\"!\"G#s!\"!\"!\"Q#s!#!\"!\"\\#s!\"!\"!\"c#s!\"!\"!"
  "\"g#s!#!\"!\"o#s!\"!\"!#\"#s!$!\"!#0#s!(!\"!#;#s!\"!\"!#B#s!\"!\"!#G#s!#!\"!#M#s!*!\"!#Y#s!#!\"!!5#t!*!\"!!B#t!#!\"!!J#t!\"!\"!!O#t!#!\"!!U#t!#!\"!!^#t!\"!\"!!o#t!#!\"!!{#t!#!\"!\"(#t!\"!\"!\"/#t!\"!\"!\"3#t!#!\"!\"<#t!\"!\"!\"@#t!\"!\"!\"G#t!\"!\"!\"Q#t!#!\"!\"\\#t!\"!\"!\"c#t!\"!\"!\"g#t!#!\"!\"o#t!\"!\"!###t!#!\"!#0#t!#!\"!#;#t!\"!\"!#B#t!\"!\"!#G#t!#!\"!#M#t!#!\"!#Y#t!\"!\"!!5#u!+!\"!!B#u!#!\"!!I#u!#!\"!!O#u!#!\"!!U#u!#!\"!!]#u!#!\"!!m#u!$!\"!!{#u!#!\"!\"(#u!\"!\"!\"/#u!\"!\"!\"3#u!#!\"!\";#u!#!\"!\"@#u!\"!\"!\"G#u!\"!\"!\"Q#u!#!\"!\"\\#u!\"!\"!\"c#u!\"!\"!\"g#u!#!\"!\"n#u!#!\"!#!#u!$!\"!#0#u!#!\"!#;#u!\"!\"!#B#u!\"!\"!#G#u!#!\"!#M#u!#!\"!#Y#u!\"!\"!!5#v!\"!\"!!=#v!#!\"!!C#v!\"!\"!!I#v!#!\"!!O#v!#!\"!!U#v!#!\"!!]#v!#!\"!!k#v!$!\"!!{#v!#!\"!\"(#v!\"!\"!\"/#v!\"!\"!\"3#v!#!\"!\";#v!#!\"!\"@#v!\"!\"!\"G#v!\"!\"!\"Q#v!#!\"!\"\\#v!\"!\"!\"c#v!\"!\"!\"h#v!\"!\"!\"n#v!#!\"!\"}#v!$!\"!#0#v!#!\"!#;#v!\"!\"!#B#v!\"!\"!#G#v!#!\"!#M#v!#!\"!#Y#v!\"!\"!!4#w!#!\"!!>#w!\"!\"!!C#w!#!\"!!I#w!#!\"!!O#w!#!\"!!V#w!#!\"!!]#w!#!\"!!i#w!$!\"!!|#w!#!\"!\"(#w!\"!\"!\"/#w!\"!\"!\"4#w!#!\"!"
  "\";#w!#!\"!\"@#w!\"!\"!\"G#w!\"!\"!\"R#w!#!\"!\"\\#w!\"!\"!\"c#w!\"!\"!\"h#w!#!\"!\"n#w!#!\"!\"{#w!$!\"!#0#w!#!\"!#;#w!\"!\"!#B#w!\"!\"!#G#w!#!\"!#N#w!#!\"!#Y#w!\"!\"!!4#x!#!\"!!>#x!#!\"!!C#x!$!\"!!G#x!%!\"!!P#x!#!\"!!V#x!%!\"!![#x!$!\"!!i#x!\"!\"!!|#x!%!\"!\"##x!#!\"!\"(#x!\"!\"!\"/#x!\"!\"!\"4#x!%!\"!\"9#x!$!\"!\"@#x!\"!\"!\"G#x!\"!\"!\"R#x!%!\"!\"W#x!#!\"!\"\\#x!\"!\"!\"c#x!\"!\"!\"h#x!#!\"!\"m#x!$!\"!\"{#x!\"!\"!#0#x!(!\"!#;#x!\"!\"!#B#x!\"!\"!#H#x!#!\"!#N#x!$!\"!#S#x!#!\"!#Y#x!\"!\"!#_#x!#!\"!!3#y!#!\"!!>#y!#!\"!!D#y!%!\"!!J#y!\"!\"!!P#y!$!\"!!X#y!%!\"!!~#y!%!\"!\"(#y!\"!\"!\"/#y!\"!\"!\"6#y!%!\"!\"@#y!\"!\"!\"G#y!\"!\"!\"T#y!%!\"!\"\\#y!\"!\"!\"c#y!\"!\"!\"i#y!%!\"!\"o#y!\"!\"!#0#y!)!\"!#;#y!\"!\"!#B#y!\"!\"!#H#y!$!\"!#P#y!%!\"!#Y#y!\"!\"!#_#y!#!\"!\"7#{!#!\"!\"7#|!#!\"!%A$5!\"!\"!%H$5!#!\"!%K$5!\"!\"!%c$5!\"!\"!&@$5!\"!\"!&G$5!\"!\"!%A$6!\"!\"!%H$6!\"!\"!%K$6!\"!\"!%c$6!\"!\"!%u$6!#!\"!%}$6!\"!\"!&*$6!(!\"!&6$6!#!\"!&<$6!#!\"!&@$6!\"!\"!&I$6!)!\"!&V$6!\"!\"!&[$6!\"!\"!%A$7!\"!\"!%G$7!\"!\"!%K$7!\"!\"!%c$7!\"!\"!%u$7!#!\"!%|$7!#!\"!&-$7!\"!\"!&6$7!$!\"!&<$7!#!\"!"
  "&@$7!\"!\"!&L$7!\"!\"!&Q$7!\"!\"!&U$7!#!\"!&Z$7!#!\"!%*$8!\"!\"!%.$8!\"!\"!%2$8!\"!\"!%5$8!%!\"!%<$8!\"!\"!%>$8!\"!\"!%A$8!\"!\"!%D$8!'!\"!%K$8!\"!\"!%O$8!%!\"!%U$8!\"!\"!%Y$8!\"!\"!%]$8!\"!\"!%c$8!\"!\"!%e$8!$!\"!%j$8!\"!\"!%o$8!\"!\"!%u$8!$!\"!%|$8!#!\"!&#$8!\"!\"!&%$8!\"!\"!&-$8!\"!\"!&6$8!\"!\"!&8$8!\"!\"!&<$8!#!\"!&@$8!\"!\"!&B$8!$!\"!&G$8!\"!\"!&L$8!\"!\"!&Q$8!\"!\"!&U$8!#!\"!&Z$8!\"!\"!%+$9!\"!\"!%.$9!#!\"!%2$9!\"!\"!%4$9!#!\"!%8$9!#!\"!%<$9!#!\"!%A$9!\"!\"!%D$9!\"!\"!%G$9!\"!\"!%K$9!\"!\"!%N$9!#!\"!%R$9!#!\"!%V$9!\"!\"!%Y$9!#!\"!%]$9!\"!\"!%c$9!#!\"!%g$9!#!\"!%k$9!\"!\"!%o$9!\"!\"!%u$9!\"!\"!%w$9!\"!\"!%|$9!#!\"!&#$9!#!\"!&-$9!\"!\"!&6$9!\"!\"!&9$9!\"!\"!&<$9!#!\"!&@$9!#!\"!&D$9!\"!\"!&G$9!\"!\"!&L$9!\"!\"!&Q$9!\"!\"!&U$9!\"!\"!&W$9!\"!\"!&Z$9!\"!\"!%+$:!\"!\"!%.$:!#!\"!%2$:!\"!\"!%4$:!\"!\"!%9$:!\"!\"!%<$:!\"!\"!%A$:!\"!\"!%C$:!\"!\"!%G$:!\"!\"!%K$:!\"!\"!%N$:!\"!\"!%S$:!\"!\"!%V$:!\"!\"!%Y$:!#!\"!%]$:!\"!\"!%c$:!\"!\"!%h$:!\"!\"!%k$:!\"!\"!%n$:!\"!\"!%u$:!\"!\"!%x$:!\"!\"!%{$:!\"!\"!%}$:!\"!\"!&#$:!\"!\"!&-$:!\"!\"!&6$:!\"!\"!&9$:!\"!\"!&<$:!#!\"!"
  "&@$:!\"!\"!&D$:!\"!\"!&G$:!\"!\"!&L$:!\"!\"!&Q$:!#!\"!&T$:!\"!\"!&W$:!\"!\"!&Z$:!\"!\"!%+$;!\"!\"!%-$;!\"!\"!%/$;!\"!\"!%1$;!\"!\"!%4$;!\"!\"!%9$;!\"!\"!%<$;!\"!\"!%A$;!#!\"!%G$;!\"!\"!%K$;!\"!\"!%N$;!\"!\"!%S$;!\"!\"!%V$;!\"!\"!%X$;!\"!\"!%Z$;!\"!\"!%\\$;!\"!\"!%c$;!\"!\"!%h$;!\"!\"!%k$;!\"!\"!%n$;!\"!\"!%u$;!\"!\"!%x$;!\"!\"!%{$;!\"!\"!%}$;!\"!\"!&#$;!\"!\"!&-$;!\"!\"!&6$;!\"!\"!&:$;!\"!\"!&<$;!#!\"!&@$;!\"!\"!&D$;!\"!\"!&G$;!\"!\"!&L$;!\"!\"!&R$;!\"!\"!&T$;!\"!\"!&W$;!\"!\"!&Y$;!\"!\"!%+$<!\"!\"!%-$<!\"!\"!%1$<!\"!\"!%4$<!\"!\"!%9$<!\"!\"!%<$<!\"!\"!%A$<!\"!\"!%C$<!\"!\"!%G$<!\"!\"!%K$<!\"!\"!%N$<!\"!\"!%S$<!\"!\"!%V$<!\"!\"!%X$<!\"!\"!%\\$<!\"!\"!%c$<!\"!\"!%h$<!\"!\"!%l$<!\"!\"!%n$<!\"!\"!%u$<!\"!\"!%x$<!\"!\"!%z$<!\"!\"!%}$<!\"!\"!&#$<!\"!\"!&-$<!\"!\"!&6$<!\"!\"!&;$<!$!\"!&@$<!\"!\"!&D$<!\"!\"!&G$<!\"!\"!&L$<!\"!\"!&R$<!\"!\"!&T$<!\"!\"!&W$<!\"!\"!&Y$<!\"!\"!%,$=!#!\"!%0$=!#!\"!%4$=!#!\"!%8$=!\"!\"!%<$=!\"!\"!%A$=!\"!\"!%D$=!\"!\"!%G$=!\"!\"!%K$=!\"!\"!%N$=!#!\"!%R$=!\"!\"!%W$=!#!\"!%[$=!#!\"!%c$=!#!\"!%g$=!\"!\"!%l$=!#!\"!%u$=!\"!\"!%y$=!#!\"!%}$=!\"!\"!&#$=!\"!\"!"
  "&($=!\"!\"!&-$=!\"!\"!&6$=!\"!\"!&;$=!$!\"!&@$=!\"!\"!&D$=!\"!\"!&G$=!\"!\"!&L$=!\"!\"!&R$=!#!\"!&X$=!#!\"!%,$>!\"!\"!%0$>!\"!\"!%5$>!$!\"!%<$>!\"!\"!%A$>!\"!\"!%E$>!\"!\"!%G$>!\"!\"!%K$>!\"!\"!%O$>!$!\"!%W$>!\"!\"!%[$>!\"!\"!%c$>!\"!\"!%e$>!$!\"!%l$>!#!\"!%u$>!\"!\"!%y$>!\"!\"!%}$>!\"!\"!&#$>!\"!\"!&($>!\"!\"!&-$>!\"!\"!&6$>!\"!\"!&<$>!#!\"!&@$>!\"!\"!&D$>!\"!\"!&G$>!\"!\"!&L$>!\"!\"!&S$>!\"!\"!&X$>!#!\"!%l$?!\"!\"!%l$@!\"!\"!%j$A!#!\"!!!!!'E!\"\"!!!\"'E!\"\"!!!#'E!\"\"!!!$'E!\"\"!3\"4#r!\"#$?\"4#q!\"#!3\"5!\"!\"#$%\"5!\"!\"#$?\"5!\"!\"#'0\"5!\"!\"#!3\"6!\"!\"#$%\"6!\"!\"#$?\"6!\"!\"#'0\"6!\"!\"#!3\"7!\"!\"#$%\"7!\"!\"#$?\"7!\"!\"#'0\"7!\"!\"#!3\"8!\"!\"#$%\"8!\"!\"#$?\"8!\"!\"#'0\"8!\"!\"#!3\"9!\"!\"#$%\"9!\"!\"#$?\"9!\"!\"#'0\"9!\"!\"#!3\":!\"!\"#$%\":!\"!\"#$?\":!\"!\"#'0\":!\"!\"#!3\";!\"!\"#$%\";!\"!\"#$?\";!\"!\"#'0\";!\"!\"#!3\"<!\"!\"#$%\"<!\"!\"#$?\"<!\"!\"#'0\"<!\"!\"#!3\"=!\"!\"#$%\"=!\"!\"#$?\"=!\"!\"#'0\"=!\"!\"#!3\">!\"!\"#$%\">!\"!\"#$?\">!\"!\"#'0\">!\"!\"#!3\"?!\"!\"#$%\"?!\"!\"#$?\"?!\"!\"#'0\"?!\"!\"#!3\"@!\"!\"#$%\"@!\"!\"#$?\"@!\"!\"#'0\"@!\"!\"#!3\"A!\"!\"#$%\"A!\"!\"#$?\"A!\"!\"#'0\"A!\"!\"#!3\"B!\"!\"#"
  "$%\"B!\"!\"#$?\"B!\"!\"#'0\"B!\"!\"#!3\"C!\"!\"#$%\"C!\"!\"#$?\"C!\"!\"#'0\"C!\"!\"#!3\"D!\"!\"#$%\"D!\"!\"#$?\"D!\"!\"#'0\"D!\"!\"#!3\"E!\"!\"#$%\"E!\"!\"#$?\"E!\"!\"#'0\"E!\"!\"#!3\"F!\"!\"#$%\"F!\"!\"#$?\"F!\"!\"#'0\"F!\"!\"#!3\"G!\"!\"#$%\"G!\"!\"#$?\"G!\"!\"#'0\"G!\"!\"#!3\"H!\"!\"#$%\"H!\"!\"#$?\"H!\"!\"#'0\"H!\"!\"#!3\"I!\"!\"#$%\"I!\"!\"#$?\"I!\"!\"#'0\"I!\"!\"#!3\"J!\"!\"#$%\"J!\"!\"#$?\"J!\"!\"#'0\"J!\"!\"#!3\"K!\"!\"#$%\"K!\"!\"#$?\"K!\"!\"#'0\"K!\"!\"#!3\"L!\"!\"#$%\"L!\"!\"#$?\"L!\"!\"#'0\"L!\"!\"#!3\"M!\"!\"#$%\"M!\"!\"#$?\"M!\"!\"#'0\"M!\"!\"#!3\"N!\"!\"#$%\"N!\"!\"#$?\"N!\"!\"#'0\"N!\"!\"#!3\"O!\"!\"#$%\"O!\"!\"#$?\"O!\"!\"#'0\"O!\"!\"#!3\"P!\"!\"#$%\"P!\"!\"#$?\"P!\"!\"#'0\"P!\"!\"#!3\"Q!\"!\"#$%\"Q!\"!\"#$?\"Q!\"!\"#'0\"Q!\"!\"#!3\"R!\"!\"#$%\"R!\"!\"#$?\"R!\"!\"#'0\"R!\"!\"#!3\"S!\"!\"#$%\"S!\"!\"#$?\"S!\"!\"#'0\"S!\"!\"#!3\"T!\"!\"#$%\"T!\"!\"#$?\"T!\"!\"#'0\"T!\"!\"#!3\"U!\"!\"#$%\"U!\"!\"#$?\"U!\"!\"#'0\"U!\"!\"#!3\"V!\"!\"#$%\"V!\"!\"#$?\"V!\"!\"#'0\"V!\"!\"#!3\"W!\"!\"#$%\"W!\"!\"#$?\"W!\"!\"#'0\"W!\"!\"#!3\"X!\"!\"#$%\"X!\"!\"#$?\"X!\"!\"#'0\"X!\"!\"#!3\"Y!\"!\"#$%\"Y!\"!\"#$?\"Y!\"!\"#'0\"Y!\"!\"#!3\"Z!\"!\"#$%\"Z!\"!\"#$?\"Z!\"!\"#'0\"Z!\"!\"#!3\"[!\"!\"#"
  "$%\"[!\"!\"#$?\"[!\"!\"#'0\"[!\"!\"#!3\"\\!\"!\"#$%\"\\!\"!\"#$?\"\\!\"!\"#'0\"\\!\"!\"#!3\"]!\"!\"#$%\"]!\"!\"#$?\"]!\"!\"#'0\"]!\"!\"#!3\"^!\"!\"#$%\"^!\"!\"#$?\"^!\"!\"#'0\"^!\"!\"#!3\"_!\"!\"#$%\"_!\"!\"#$?\"_!\"!\"#'0\"_!\"!\"#!3\"`!\"!\"#$%\"`!\"!\"#$?\"`!\"!\"#'0\"`!\"!\"#!3\"a!\"!\"#$%\"a!\"!\"#$?\"a!\"!\"#'0\"a!\"!\"#!3\"b!\"!\"#$%\"b!\"!\"#$?\"b!\"!\"#'0\"b!\"!\"#!3\"c!\"!\"#$%\"c!\"!\"#$?\"c!\"!\"#'0\"c!\"!\"#!3\"d!\"!\"#$%\"d!\"!\"#$?\"d!\"!\"#'0\"d!\"!\"#!3\"e!\"!\"#$%\"e!\"!\"#$?\"e!\"!\"#'0\"e!\"!\"#!3\"f!\"!\"#$%\"f!\"!\"#$?\"f!\"!\"#'0\"f!\"!\"#!3\"g!\"!\"#$%\"g!\"!\"#$?\"g!\"!\"#'0\"g!\"!\"#!3\"h!\"!\"#$%\"h!\"!\"#$?\"h!\"!\"#'0\"h!\"!\"#!3\"i!\"!\"#$%\"i!\"!\"#$?\"i!\"!\"#'0\"i!\"!\"#!3\"j!\"!\"#$%\"j!\"!\"#$?\"j!\"!\"#'0\"j!\"!\"#!3\"k!\"!\"#$%\"k!\"!\"#$?\"k!\"!\"#'0\"k!\"!\"#!3\"l!\"!\"#$%\"l!\"!\"#$?\"l!\"!\"#'0\"l!\"!\"#!3\"m!\"!\"#$%\"m!\"!\"#$?\"m!\"!\"#'0\"m!\"!\"#!3\"n!\"!\"#$%\"n!\"!\"#$?\"n!\"!\"#'0\"n!\"!\"#!3\"o!\"!\"#$%\"o!\"!\"#$?\"o!\"!\"#'0\"o!\"!\"#!3\"p!\"!\"#$%\"p!\"!\"#$?\"p!\"!\"#'0\"p!\"!\"#!3\"q!\"!\"#$%\"q!\"!\"#$?\"q!\"!\"#'0\"q!\"!\"#!3\"r!\"!\"#$%\"r!\"!\"#$?\"r!\"!\"#'0\"r!\"!\"#!3\"s!\"!\"#$%\"s!\"!\"#$?\"s!\"!\"#'0\"s!\"!\"#!3\"t!\"!\"#"
  "$%\"t!\"!\"#$?\"t!\"!\"#'0\"t!\"!\"#!3\"u!\"!\"#$%\"u!\"!\"#$?\"u!\"!\"#'0\"u!\"!\"#!3\"v!\"!\"#$%\"v!\"!\"#$?\"v!\"!\"#'0\"v!\"!\"#!3\"w!\"!\"#$%\"w!\"!\"#$?\"w!\"!\"#'0\"w!\"!\"#!3\"x!\"!\"#$%\"x!\"!\"#$?\"x!\"!\"#'0\"x!\"!\"#!3\"y!\"!\"#$%\"y!\"!\"#$?\"y!\"!\"#'0\"y!\"!\"#!3\"z!\"!\"#$%\"z!\"!\"#$?\"z!\"!\"#'0\"z!\"!\"#!3\"{!\"!\"#$%\"{!\"!\"#$?\"{!\"!\"#'0\"{!\"!\"#!3\"|!\"!\"#$%\"|!\"!\"#$?\"|!\"!\"#'0\"|!\"!\"#!3\"}!\"!\"#$%\"}!\"!\"#$?\"}!\"!\"#'0\"}!\"!\"#!3\"~!\"!\"#$%\"~!\"!\"#$?\"~!\"!\"#'0\"~!\"!\"#!3#!!\"!\"#$%#!!\"!\"#$?#!!\"!\"#'0#!!\"!\"#!3#\"!\"!\"#$%#\"!\"!\"#$?#\"!\"!\"#'0#\"!\"!\"#!3##!\"!\"#$%##!\"!\"#$?##!\"!\"#'0##!\"!\"#!3#$!\"!\"#$%#$!\"!\"#$?#$!\"!\"#'0#$!\"!\"#!3#%!\"!\"#$%#%!\"!\"#$?#%!\"!\"#'0#%!\"!\"#!3#&!\"!\"#$%#&!\"!\"#$?#&!\"!\"#'0#&!\"!\"#!3#'!\"!\"#$%#'!\"!\"#$?#'!\"!\"#'0#'!\"!\"#!3#(!\"!\"#$%#(!\"!\"#$?#(!\"!\"#'0#(!\"!\"#!3#)!\"!\"#$%#)!\"!\"#$?#)!\"!\"#'0#)!\"!\"#!3#*!\"!\"#$%#*!\"!\"#$?#*!\"!\"#'0#*!\"!\"#!3#+!\"!\"#$%#+!\"!\"#$?#+!\"!\"#'0#+!\"!\"#!3#,!\"!\"#$%#,!\"!\"#$?#,!\"!\"#'0#,!\"!\"#!3#-!\"!\"#$%#-!\"!\"#$?#-!\"!\"#'0#-!\"!\"#!3#.!\"!\"#$%#.!\"!\"#$?#.!\"!\"#'0#.!\"!\"#!3#/!\"!\"#"
  "$%#/!\"!\"#$?#/!\"!\"#'0#/!\"!\"#!3#0#r!\"#$?#0#q!\"#"
 ))
 (list 720 385 (strcat
  "!B!D!)!\"!!X!D!0!\"!!}!D!*!\"!\"4!D!(!\"!\"G!D!2!\"!\"f!D!)!\"!\"}!D!)!\"!#4!D!'!\"!#>!D!8!\"!!A!E!*!\"!!X!E!3!\"!!z!E!/!\"!\"1!E!.!\"!\"G!E!2!\"!\"c!E!/!\"!\"}!E!)!\"!#4!E!'!\"!#>!E!8!\"!!A!F!+!\"!!X!F!4!\"!!x!F!1!\"!\"/!F!1!\"!\"G!F!2!\"!\"b!F!2!\"!\"}!F!*!\"!#4!F!'!\"!#>!F!8!\"!!A!G!+!\"!!X!G!5!\"!!w!G!2!\"!\"/!G!2!\"!\"G!G!2!\"!\"`!G!5!\"!\"}!G!+!\"!#4!G!'!\"!#>!G!8!\"!!@!H!,!\"!!X!H!6!\"!!v!H!3!\"!\"/!H!2!\"!\"G!H!2!\"!\"_!H!7!\"!\"}!H!+!\"!#4!H!'!\"!#>!H!8!\"!!@!I!-!\"!!X!I!(!\"!!e!I!)!\"!!u!I!4!\"!\"/!I!&!\"!\"8!I!*!\"!\"G!I!(!\"!\"_!I!8!\"!\"}!I!,!\"!#4!I!'!\"!#F!I!(!\"!!@!J!-!\"!!X!J!(!\"!!f!J!(!\"!!t!J!+!\"!\")!J!\"!\"!\"/!J!#!\"!\":!J!(!\"!\"G!J!(!\"!\"^!J!*!\"!\"m!J!*!\"!\"}!J!-!\"!#4!J!'!\"!#F!J!(!\"!!?!K!'!\"!!F!K!'!\"!!X!K!(!\"!!f!K!(!\"!!s!K!*!\"!\"/!K!\"!\"!\":!K!(!\"!\"G!K!(!\"!\"]!K!)!\"!\"o!K!)!\"!\"}!K!-!\"!#4!K!'!\"!#F!K!(!\"!!?!L!'!\"!!F!L!(!\"!!X!L!(!\"!!f!L!(!\"!!s!L!)!\"!\";!L!'!\"!\"G!L!(!\"!\"]!L!(!\"!\"p!L!(!\"!\"}!L!.!\"!#4!L!'!\"!#F!L!(!\"!!?!M!'!\"!!G!M!'!\"!!X!M!(!\"!!f!M!(!\"!!s!M!(!\"!\";!M!'!\"!"
  "\"G!M!(!\"!\"\\!M!)!\"!\"p!M!)!\"!\"}!M!/!\"!#4!M!'!\"!#F!M!(!\"!';!M!#!\"!'D!M!#!\"!'K!M!\"!\"!'W!M!%!\"!'f!M!%!\"!!>!N!'!\"!!G!N!'!\"!!X!N!(!\"!!f!N!'!\"!!r!N!(!\"!\":!N!(!\"!\"G!N!(!\"!\"\\!N!(!\"!\"q!N!(!\"!\"}!N!(!\"!#'!N!'!\"!#4!N!'!\"!#F!N!(!\"!'<!N!\"!\"!'D!N!\"!\"!'J!N!#!\"!'V!N!#!\"!'Z!N!#!\"!'e!N!#!\"!'i!N!\"!\"!!>!O!'!\"!!G!O!(!\"!!X!O!(!\"!!d!O!(!\"!!r!O!(!\"!\":!O!(!\"!\"G!O!(!\"!\"\\!O!(!\"!\"q!O!(!\"!\"}!O!(!\"!#'!O!(!\"!#4!O!'!\"!#F!O!(!\"!'<!O!#!\"!'D!O!\"!\"!'H!O!%!\"!'U!O!#!\"!'[!O!\"!\"!'d!O!#!\"!!=!P!(!\"!!H!P!'!\"!!X!P!3!\"!!r!P!(!\"!\"9!P!(!\"!\"G!P!(!\"!\"\\!P!(!\"!\"q!P!(!\"!\"}!P!(!\"!#(!P!'!\"!#4!P!'!\"!#F!P!(!\"!'<!P!#!\"!'C!P!#!\"!'H!P!\"!\"!'K!P!\"!\"!'U!P!\"!\"!'[!P!#!\"!'d!P!\"!\"!!=!Q!'!\"!!H!Q!'!\"!!X!Q!1!\"!!r!Q!(!\"!\"8!Q!)!\"!\"G!Q!2!\"!\"\\!Q!(!\"!\"q!Q!(!\"!\"}!Q!(!\"!#)!Q!'!\"!#4!Q!'!\"!#F!Q!(!\"!'=!Q!\"!\"!'C!Q!\"!\"!'K!Q!\"!\"!'U!Q!\"!\"!'[!Q!#!\"!'c!Q!#!\"!!=!R!'!\"!!H!R!(!\"!!X!R!3!\"!!r!R!(!\"!\"7!R!)!\"!\"G!R!2!\"!\"\\!R!(!\"!\"q!R!(!\"!\"}!R!(!\"!#)!R!(!\"!#4!R!'!\"!#F!R!(!\"!'=!R!#!\"!"
  "'C!R!\"!\"!'K!R!\"!\"!'U!R!#!\"!'[!R!#!\"!'c!R!#!\"!'f!R!$!\"!!<!S!(!\"!!I!S!'!\"!!X!S!5!\"!!r!S!(!\"!\"6!S!)!\"!\"G!S!2!\"!\"\\!S!(!\"!\"q!S!(!\"!\"}!S!(!\"!#*!S!'!\"!#4!S!'!\"!#F!S!(!\"!'=!S!#!\"!'B!S!#!\"!'K!S!\"!\"!'V!S!#!\"!'Z!S!$!\"!'c!S!$!\"!'h!S!#!\"!!<!T!'!\"!!I!T!(!\"!!X!T!6!\"!!r!T!(!\"!\"5!T!)!\"!\"G!T!2!\"!\"\\!T!(!\"!\"q!T!(!\"!\"}!T!(!\"!#*!T!(!\"!#4!T!'!\"!#F!T!(!\"!'>!T!\"!\"!'B!T!\"!\"!'K!T!\"!\"!'W!T!$!\"!'[!T!#!\"!'c!T!#!\"!'i!T!#!\"!!<!U!'!\"!!I!U!(!\"!!X!U!(!\"!!e!U!*!\"!!r!U!(!\"!\"3!U!*!\"!\"G!U!2!\"!\"\\!U!(!\"!\"q!U!(!\"!\"}!U!(!\"!#+!U!(!\"!#4!U!'!\"!#F!U!(!\"!'>!U!#!\"!'A!U!#!\"!'K!U!\"!\"!'[!U!#!\"!'c!U!#!\"!'i!U!#!\"!!;!V!6!\"!!X!V!(!\"!!g!V!(!\"!!r!V!(!\"!\"2!V!*!\"!\"G!V!(!\"!\"\\!V!(!\"!\"q!V!(!\"!\"}!V!(!\"!#,!V!'!\"!#4!V!'!\"!#F!V!(!\"!'>!V!#!\"!'A!V!#!\"!'K!V!\"!\"!'[!V!\"!\"!'c!V!#!\"!'i!V!#!\"!!;!W!7!\"!!X!W!(!\"!!g!W!(!\"!!r!W!(!\"!\"1!W!*!\"!\"G!W!(!\"!\"\\!W!(!\"!\"q!W!(!\"!\"}!W!(!\"!#,!W!(!\"!#4!W!'!\"!#F!W!(!\"!'?!W!$!\"!'K!W!\"!\"!'Z!W!#!\"!'d!W!\"!\"!'i!W!#!\"!!;!X!7!\"!!X!X!(!\"!"
  "!g!X!(!\"!!r!X!)!\"!\"0!X!)!\"!\"G!X!(!\"!\"\\!X!)!\"!\"p!X!(!\"!\"}!X!(!\"!#-!X!.!\"!#F!X!(!\"!'?!X!$!\"!'K!X!\"!\"!'Q!X!#!\"!'V!X!\"!\"!'Y!X!#!\"!'_!X!#!\"!'d!X!#!\"!'h!X!#!\"!!:!Y!8!\"!!X!Y!(!\"!!g!Y!(!\"!!s!Y!)!\"!\"0!Y!(!\"!\"G!Y!(!\"!\"]!Y!(!\"!\"p!Y!(!\"!\"}!Y!(!\"!#.!Y!-!\"!#F!Y!(!\"!'@!Y!#!\"!'K!Y!\"!\"!'Q!Y!#!\"!'V!Y!%!\"!'_!Y!#!\"!'e!Y!%!\"!!:!Z!9!\"!!X!Z!(!\"!!g!Z!(!\"!!s!Z!*!\"!\"/!Z!(!\"!\"G!Z!(!\"!\"]!Z!)!\"!\"o!Z!)!\"!\"}!Z!(!\"!#.!Z!-!\"!#F!Z!(!\"!!:![!'!\"!!K![!(!\"!!X![!(!\"!!g![!(!\"!!s![!+!\"!\"(![!#!\"!\"/![!'!\"!\"G![!(!\"!\"^![!*!\"!\"m![!*!\"!\"}![!(!\"!#/![!,!\"!#F![!(!\"!!9!\\!(!\"!!K!\\!(!\"!!X!\\!(!\"!!e!\\!*!\"!!t!\\!5!\"!\".!\\!(!\"!\"G!\\!(!\"!\"^!\\!8!\"!\"}!\\!(!\"!#0!\\!+!\"!#F!\\!(!\"!!9!]!(!\"!!L!]!(!\"!!X!]!6!\"!!u!]!4!\"!\".!]!4!\"!\"G!]!(!\"!\"_!]!7!\"!\"}!]!(!\"!#0!]!+!\"!#F!]!(!\"!!8!^!(!\"!!L!^!(!\"!!X!^!6!\"!!v!^!3!\"!\".!^!4!\"!\"G!^!(!\"!\"`!^!5!\"!\"}!^!(!\"!#1!^!*!\"!#F!^!(!\"!!8!_!(!\"!!L!_!(!\"!!X!_!5!\"!!w!_!2!\"!\".!_!4!\"!\"G!_!(!\"!\"a!_!2!\"!\"}!_!(!\"!#2!_!)!\"!#F!_!(!\"!!8!`!(!\"!"
  "!M!`!(!\"!!X!`!3!\"!!y!`!0!\"!\".!`!4!\"!\"G!`!(!\"!\"c!`!/!\"!\"}!`!(!\"!#2!`!)!\"!#F!`!(!\"!!7!a!)!\"!!M!a!(!\"!!X!a!1!\"!!|!a!*!\"!\".!a!4!\"!\"G!a!(!\"!\"f!a!)!\"!\"}!a!(!\"!#3!a!(!\"!#F!a!(!\"!$`!t!$!\"!$b!u!\"!\"!$b!v!\"!\"!#m!w!#!\"!$7!w!#!\"!$R!w!#!\"!$`!w!$!\"!$w!w!$!\"!!7!x!.!\"!!K!x!'!\"!!S!x!$!\"!!`!x!#!\"!!e!x!$!\"!!q!x!#!\"!!y!x!'!\"!\"-!x!#!\"!\"6!x!#!\"!\"A!x!)!\"!\"T!x!'!\"!#$!x!#!\"!#/!x!#!\"!#C!x!#!\"!#m!x!#!\"!$8!x!#!\"!$R!x!#!\"!$\\!x!$!\"!$`!x!#!\"!$g!x!#!\"!$v!x!%!\"!!8!y!-!\"!!I!y!*!\"!!T!y!#!\"!!`!y!#!\"!!e!y!%!\"!!q!y!#!\"!!x!y!)!\"!\",!y!#!\"!\"5!y!%!\"!\"A!y!*!\"!\"R!y!*!\"!#$!y!#!\"!#/!y!#!\"!#C!y!#!\"!#m!y!#!\"!$8!y!#!\"!$N!y!(!\"!$[!y!#!\"!$^!y!#!\"!$g!y!#!\"!$u!y!#!\"!%<!y!#!\"!!=!z!#!\"!!H!z!$!\"!!Q!z!\"!\"!!T!z!#!\"!!_!z!#!\"!!e!z!&!\"!!q!z!#!\"!!x!z!\"!\"!!~!z!$!\"!\",!z!#!\"!\"5!z!%!\"!\"A!z!#!\"!\"H!z!$!\"!\"Q!z!$!\"!\"Z!z!\"!\"!#$!z!#!\"!#/!z!#!\"!#m!z!#!\"!$9!z!#!\"!$N!z!(!\"!$[!z!\"!\"!$_!z!#!\"!$u!z!#!\"!%<!z!#!\"!!=!{!#!\"!!G!{!$!\"!!T!{!$!\"!!_!{!#!\"!!e!{!&!\"!!q!{!#!\"!"
  "\"!!{!#!\"!\",!{!#!\"!\"5!{!%!\"!\"A!{!#!\"!\"I!{!#!\"!\"P!{!$!\"!#$!{!#!\"!#/!{!#!\"!#m!{!#!\"!$R!{!#!\"!$u!{!#!\"!%<!{!#!\"!!=!|!#!\"!!F!|!$!\"!!U!|!#!\"!!_!|!#!\"!!e!|!#!\"!!h!|!$!\"!!q!|!#!\"!\"!!|!#!\"!\"+!|!#!\"!\"4!|!#!\"!\"8!|!#!\"!\"A!|!#!\"!\"I!|!#!\"!\"O!|!$!\"!#$!|!#!\"!#/!|!#!\"!#m!|!#!\"!$R!|!#!\"!$u!|!#!\"!%<!|!#!\"!!=!}!#!\"!!F!}!#!\"!!U!}!#!\"!!^!}!#!\"!!e!}!#!\"!!i!}!#!\"!!q!}!#!\"!\"!!}!#!\"!\"+!}!#!\"!\"4!}!#!\"!\"8!}!#!\"!\"A!}!#!\"!\"I!}!#!\"!\"O!}!#!\"!#$!}!#!\"!#/!}!#!\"!#5!}!#!\"!#9!}!%!\"!#C!}!#!\"!#L!}!&!\"!#X!}!&!\"!#g!}!%!\"!#m!}!#!\"!#v!}!%!\"!$'!}!$!\"!$1!}!#!\"!$7!}!&!\"!$L!}!%!\"!$R!}!#!\"!$[!}!&!\"!$g!}!#!\"!$s!}!(!\"!%!!}!&!\"!%-!}!#!\"!%1!}!%!\"!%:!}!(!\"!!=!~!#!\"!!F!~!#!\"!!V!~!#!\"!!^!~!#!\"!!e!~!#!\"!!j!~!#!\"!!q!~!#!\"!!~!~!#!\"!\"*!~!#!\"!\"4!~!#!\"!\"8!~!$!\"!\"A!~!#!\"!\"I!~!#!\"!\"O!~!#!\"!#$!~!#!\"!#/!~!#!\"!#5!~!#!\"!#8!~!'!\"!#C!~!#!\"!#J!~!)!\"!#V!~!*!\"!#e!~!+!\"!#t!~!)!\"!$(!~!#!\"!$0!~!#!\"!$5!~!)!\"!$J!~!+!\"!$Y!~!*!\"!$g!~!#!\"!$s!~!'!\"!$}!~!*!\"!%-!~!#!\"!"
  "%0!~!'!\"!%:!~!(!\"!!=\"!!#!\"!!E\"!!#!\"!!V\"!!#!\"!!]\"!!$!\"!!e\"!!#!\"!!j\"!!$!\"!!q\"!!#!\"!!}\"!!$!\"!\"*\"!!#!\"!\"3\"!!#!\"!\"9\"!!#!\"!\"A\"!!#!\"!\"G\"!!$!\"!\"N\"!!#!\"!\"s\"!!\"!\"!#$\"!!#!\"!#/\"!!#!\"!#5\"!!$!\"!#<\"!!$!\"!#C\"!!#!\"!#I\"!!$!\"!#U\"!!$!\"!#]\"!!#!\"!#d\"!!$!\"!#k\"!!%!\"!#s\"!!$!\"!#z\"!!#!\"!$(\"!!#!\"!$0\"!!#!\"!$5\"!!\"!\"!$;\"!!$!\"!$I\"!!$!\"!$P\"!!%!\"!$X\"!!$!\"!$`\"!!#!\"!$g\"!!#!\"!$u\"!!#!\"!$|\"!!$!\"!%&\"!!#!\"!%-\"!!$!\"!%4\"!!$!\"!%<\"!!#!\"!!=\"\"!#!\"!!E\"\"!#!\"!!V\"\"!#!\"!!]\"\"!#!\"!!e\"\"!#!\"!!k\"\"!#!\"!!q\"\"!#!\"!!y\"\"!&!\"!\"*\"\"!#!\"!\"3\"\"!#!\"!\"9\"\"!#!\"!\"A\"\"!)!\"!\"N\"\"!#!\"!\"s\"\"!\"!\"!#$\"\"!#!\"!#/\"\"!#!\"!#5\"\"!$!\"!#=\"\"!#!\"!#C\"\"!#!\"!#I\"\"!#!\"!#U\"\"!#!\"!#^\"\"!#!\"!#d\"\"!#!\"!#l\"\"!$!\"!#s\"\"!#!\"!#{\"\"!#!\"!$(\"\"!$!\"!$0\"\"!#!\"!$<\"\"!#!\"!$I\"\"!#!\"!$Q\"\"!$!\"!$X\"\"!#!\"!$a\"\"!#!\"!$g\"\"!#!\"!$u\"\"!#!\"!$|\"\"!#!\"!%'\"\"!#!\"!%-\"\"!$!\"!%5\"\"!#!\"!%<\"\"!#!\"!!=\"#!#!\"!!E\"#!#!\"!!W\"#!#!\"!!]\"#!#!\"!!e\"#!#!\"!!l\"#!#!\"!!q\"#!#!\"!!y\"#!(!\"!\")\"#!#!\"!\"2\"#!$!\"!\":\"#!#!\"!\"A\"#!*!\"!\"N\"#!#!\"!\"t\"#!\"!\"!#$\"#!#!\"!#/\"#!#!\"!"
  "#5\"#!#!\"!#=\"#!#!\"!#C\"#!#!\"!#H\"#!$!\"!#T\"#!$!\"!#^\"#!#!\"!#c\"#!$!\"!#m\"#!#!\"!#r\"#!#!\"!#{\"#!#!\"!$)\"#!#!\"!$/\"#!#!\"!$<\"#!#!\"!$H\"#!$!\"!$R\"#!#!\"!$W\"#!$!\"!$a\"#!#!\"!$g\"#!#!\"!$u\"#!#!\"!${\"#!$!\"!%'\"#!#!\"!%-\"#!#!\"!%5\"#!#!\"!%<\"#!#!\"!!=\"$!#!\"!!E\"$!#!\"!!W\"$!#!\"!!\\\"$!$!\"!!e\"$!#!\"!!l\"$!#!\"!!q\"$!#!\"!!~\"$!$!\"!\")\"$!#!\"!\"2\"$!#!\"!\":\"$!#!\"!\"A\"$!#!\"!\"I\"$!#!\"!\"N\"$!#!\"!\"f\"$!1!\"!#$\"$!#!\"!#/\"$!#!\"!#5\"$!#!\"!#=\"$!#!\"!#C\"$!#!\"!#H\"$!#!\"!#T\"$!#!\"!#^\"$!#!\"!#c\"$!#!\"!#m\"$!#!\"!#r\"$!,!\"!$)\"$!#!\"!$/\"$!#!\"!$8\"$!'!\"!$H\"$!#!\"!$R\"$!#!\"!$W\"$!#!\"!$a\"$!#!\"!$g\"$!#!\"!$u\"$!#!\"!${\"$!#!\"!%'\"$!#!\"!%-\"$!#!\"!%5\"$!#!\"!%<\"$!#!\"!!=\"%!#!\"!!E\"%!$!\"!!W\"%!#!\"!!\\\"%!#!\"!!e\"%!#!\"!!m\"%!#!\"!!q\"%!#!\"!\"!\"%!#!\"!\"(\"%!#!\"!\"2\"%!+!\"!\"A\"%!#!\"!\"J\"%!#!\"!\"N\"%!$!\"!\"f\"%!1!\"!#$\"%!#!\"!#/\"%!#!\"!#5\"%!#!\"!#=\"%!#!\"!#C\"%!#!\"!#H\"%!#!\"!#T\"%!#!\"!#^\"%!#!\"!#c\"%!#!\"!#m\"%!#!\"!#r\"%!,!\"!$*\"%!#!\"!$.\"%!#!\"!$6\"%!)!\"!$H\"%!#!\"!$R\"%!#!\"!$W\"%!#!\"!$a\"%!#!\"!$g\"%!#!\"!$u\"%!#!\"!${\"%!#!\"!%'\"%!#!\"!"
  "%-\"%!#!\"!%5\"%!#!\"!%<\"%!#!\"!!=\"&!#!\"!!F\"&!#!\"!!X\"&!#!\"!!\\\"&!#!\"!!e\"&!#!\"!!m\"&!$!\"!!q\"&!#!\"!\"\"\"&!#!\"!\"(\"&!#!\"!\"1\"&!-!\"!\"A\"&!#!\"!\"J\"&!#!\"!\"O\"&!#!\"!\"t\"&!\"!\"!#$\"&!#!\"!#/\"&!#!\"!#5\"&!#!\"!#=\"&!#!\"!#C\"&!#!\"!#H\"&!#!\"!#T\"&!#!\"!#^\"&!#!\"!#c\"&!#!\"!#m\"&!#!\"!#r\"&!#!\"!$*\"&!#!\"!$.\"&!#!\"!$5\"&!#!\"!$<\"&!#!\"!$H\"&!#!\"!$R\"&!#!\"!$W\"&!#!\"!$a\"&!#!\"!$g\"&!#!\"!$u\"&!#!\"!${\"&!#!\"!%'\"&!#!\"!%-\"&!#!\"!%5\"&!#!\"!%<\"&!#!\"!!=\"'!#!\"!!F\"'!#!\"!!X\"'!#!\"!![\"'!#!\"!!e\"'!#!\"!!n\"'!#!\"!!q\"'!#!\"!\"\"\"'!#!\"!\"(\"'!#!\"!\"1\"'!#!\"!\";\"'!#!\"!\"A\"'!#!\"!\"J\"'!#!\"!\"O\"'!#!\"!\"s\"'!\"!\"!#$\"'!$!\"!#/\"'!#!\"!#5\"'!#!\"!#=\"'!#!\"!#C\"'!#!\"!#H\"'!$!\"!#T\"'!$!\"!#^\"'!#!\"!#c\"'!#!\"!#l\"'!$!\"!#r\"'!$!\"!$*\"'!#!\"!$.\"'!#!\"!$4\"'!#!\"!$<\"'!#!\"!$H\"'!#!\"!$Q\"'!$!\"!$W\"'!$!\"!$a\"'!#!\"!$g\"'!#!\"!$u\"'!#!\"!${\"'!$!\"!%'\"'!#!\"!%-\"'!#!\"!%5\"'!#!\"!%<\"'!#!\"!!=\"(!#!\"!!G\"(!#!\"!!X\"(!#!\"!![\"(!#!\"!!e\"(!#!\"!!o\"(!%!\"!\"!\"(!#!\"!\"'\"(!#!\"!\"1\"(!#!\"!\";\"(!$!\"!\"A\"(!#!\"!\"I\"(!$!\"!\"P\"(!#!\"!\"s\"(!\"!\"!#%\"(!#!\"!#.\"(!#!\"!"
  "#5\"(!#!\"!#=\"(!#!\"!#C\"(!#!\"!#I\"(!#!\"!#U\"(!#!\"!#^\"(!#!\"!#d\"(!#!\"!#l\"(!$!\"!#s\"(!#!\"!$+\"(!%!\"!$4\"(!#!\"!$<\"(!#!\"!$I\"(!#!\"!$Q\"(!$!\"!$X\"(!#!\"!$a\"(!#!\"!$g\"(!#!\"!$u\"(!#!\"!$|\"(!#!\"!%'\"(!#!\"!%-\"(!#!\"!%5\"(!#!\"!%<\"(!#!\"!!=\")!#!\"!!G\")!%!\"!!Q\")!\"!\"!!Y\")!%!\"!!e\")!#!\"!!o\")!%!\"!!~\")!$!\"!\"'\")!#!\"!\"0\")!#!\"!\"<\")!#!\"!\"A\")!#!\"!\"I\")!#!\"!\"P\")!%!\"!\"Z\")!\"!\"!#%\")!$!\"!#-\")!$!\"!#5\")!#!\"!#=\")!#!\"!#C\")!#!\"!#I\")!$!\"!#U\")!$!\"!#]\")!#!\"!#d\")!$!\"!#k\")!%!\"!#s\")!$!\"!#{\")!\"!\"!$+\")!%!\"!$4\")!$!\"!$;\")!$!\"!$I\")!$!\"!$P\")!%!\"!$X\")!$!\"!$`\")!#!\"!$g\")!#!\"!$u\")!#!\"!$|\")!$!\"!%&\")!#!\"!%-\")!#!\"!%5\")!#!\"!%<\")!#!\"!!=\"*!#!\"!!H\"*!+!\"!!Y\"*!$!\"!!e\"*!#!\"!!p\"*!$!\"!!w\"*!*!\"!\"&\"*!#!\"!\"0\"*!#!\"!\"<\"*!#!\"!\"A\"*!+!\"!\"Q\"*!+!\"!#&\"*!*!\"!#5\"*!#!\"!#=\"*!#!\"!#C\"*!#!\"!#J\"*!)!\"!#V\"*!)!\"!#e\"*!(!\"!#m\"*!#!\"!#t\"*!)!\"!$+\"*!$!\"!$5\"*!'!\"!$<\"*!#!\"!$J\"*!(!\"!$R\"*!#!\"!$Y\"*!)!\"!$g\"*!#!\"!$u\"*!#!\"!$}\"*!)!\"!%-\"*!#!\"!%5\"*!#!\"!%<\"*!&!\"!!=\"+!#!\"!!J\"+!(!\"!!Y\"+!$!\"!!e\"+!#!\"!!q\"+!#!\"!"
  "!x\"+!'!\"!\"&\"+!#!\"!\"/\"+!$!\"!\"<\"+!$!\"!\"A\"+!)!\"!\"S\"+!(!\"!#(\"+!&!\"!#5\"+!#!\"!#=\"+!#!\"!#C\"+!#!\"!#L\"+!&!\"!#X\"+!&!\"!#f\"+!&!\"!#m\"+!#!\"!#u\"+!'!\"!$,\"+!#!\"!$6\"+!%!\"!$<\"+!#!\"!$K\"+!&!\"!$R\"+!#!\"!$[\"+!&!\"!$g\"+!#!\"!$u\"+!#!\"!%!\"+!&!\"!%-\"+!#!\"!%5\"+!#!\"!%=\"+!%!\"!\"&\",!#!\"!\"%\"-!#!\"!\"%\".!#!\"!!m\"`!%!\"!\"M\"`!%!\"!\"k\"`!$!\"!\"v\"`!&!\"!%R\"`!$!\"!%i\"`!%!\"!!J\"a!,!\"!!m\"a!%!\"!\"M\"a!%!\"!\"j\"a!%!\"!\"u\"a!'!\"!%6\"a!'!\"!%Q\"a!%!\"!%i\"a!%!\"!!J\"b!.!\"!!m\"b!%!\"!\"M\"b!%!\"!\"j\"b!&!\"!\"t\"b!(!\"!%6\"b!'!\"!%Q\"b!&!\"!%i\"b!%!\"!!J\"c!/!\"!!m\"c!%!\"!\"M\"c!%!\"!\"j\"c!%!\"!\"s\"c!&!\"!#\"\"c!#!\"!%6\"c!(!\"!%Q\"c!%!\"!%i\"c!%!\"!!J\"d!0!\"!!m\"d!%!\"!\"M\"d!%!\"!\"k\"d!$!\"!\"s\"d!&!\"!\"~\"d!%!\"!%5\"d!)!\"!%R\"d!$!\"!%i\"d!%!\"!!J\"e!&!\"!!S\"e!'!\"!!m\"e!%!\"!\"M\"e!%!\"!\"s\"e!&!\"!\"~\"e!%!\"!%5\"e!)!\"!%i\"e!%!\"!!J\"f!&!\"!!T\"f!&!\"!!m\"f!%!\"!\"M\"f!%!\"!\"s\"f!&!\"!\"~\"f!%!\"!%5\"f!%!\"!%:\"f!%!\"!%i\"f!%!\"!!J\"g!&!\"!!T\"g!%!\"!!_\"g!(!\"!!m\"g!%!\"!!s\"g!&!\"!!~\"g!%!\"!\"'\"g!%!\"!\"4\"g!(!\"!\"C\"g!'!\"!\"M\"g!%!\"!\"S\"g!&!\"!\"^\"g!%!\"!\"d\"g!$!\"!"
  "\"j\"g!%!\"!\"q\"g!+!\"!\"|\"g!+!\"!%4\"g!%!\"!%:\"g!%!\"!%E\"g!%!\"!%K\"g!$!\"!%Q\"g!%!\"!%[\"g!(!\"!%i\"g!%!\"!!J\"h!&!\"!!S\"h!&!\"!!]\"h!+!\"!!m\"h!%!\"!!r\"h!)!\"!!~\"h!%!\"!\"%\"h!)!\"!\"2\"h!*!\"!\"A\"h!*!\"!\"M\"h!%!\"!\"R\"h!)!\"!\"^\"h!%!\"!\"c\"h!%!\"!\"j\"h!%!\"!\"q\"h!+!\"!\"|\"h!+!\"!%4\"h!%!\"!%:\"h!&!\"!%E\"h!%!\"!%J\"h!%!\"!%Q\"h!%!\"!%Y\"h!+!\"!%i\"h!%!\"!!J\"i!.!\"!!]\"i!,!\"!!m\"i!.!\"!!~\"i!.!\"!\"2\"i!*!\"!\"@\"i!+!\"!\"M\"i!.!\"!\"^\"i!*!\"!\"j\"i!%!\"!\"q\"i!+!\"!\"|\"i!+!\"!%4\"i!%!\"!%:\"i!&!\"!%E\"i!*!\"!%Q\"i!%!\"!%Y\"i!,!\"!%i\"i!%!\"!!J\"j!-!\"!!]\"j!$!\"!!d\"j!&!\"!!m\"j!'!\"!!u\"j!&!\"!!~\"j!'!\"!\"(\"j!'!\"!\"1\"j!&!\"!\":\"j!\"!\"!\"?\"j!'!\"!\"I\"j!\"!\"!\"M\"j!'!\"!\"U\"j!&!\"!\"^\"j!*!\"!\"j\"j!%!\"!\"s\"j!&!\"!\"~\"j!%!\"!%3\"j!&!\"!%;\"j!%!\"!%E\"j!*!\"!%Q\"j!%!\"!%Y\"j!$!\"!%`\"j!&!\"!%i\"j!%!\"!!J\"k!/!\"!!]\"k!\"!\"!!e\"k!%!\"!!m\"k!&!\"!!v\"k!&!\"!!~\"k!&!\"!\")\"k!&!\"!\"1\"k!%!\"!\"?\"k!%!\"!\"M\"k!&!\"!\"V\"k!&!\"!\"^\"k!'!\"!\"j\"k!%!\"!\"s\"k!&!\"!\"~\"k!%!\"!%3\"k!%!\"!%;\"k!&!\"!%E\"k!'!\"!%Q\"k!%!\"!%Y\"k!\"!\"!%a\"k!%!\"!%i\"k!%!\"!!J\"l!0!\"!!d\"l!&!\"!!m\"l!%!\"!!v\"l!&!\"!"
  "!~\"l!%!\"!\")\"l!&!\"!\"1\"l!'!\"!\">\"l!&!\"!\"M\"l!%!\"!\"V\"l!&!\"!\"^\"l!&!\"!\"j\"l!%!\"!\"s\"l!&!\"!\"~\"l!%!\"!%2\"l!&!\"!%;\"l!&!\"!%E\"l!&!\"!%Q\"l!%!\"!%`\"l!&!\"!%i\"l!%!\"!!J\"m!&!\"!!T\"m!&!\"!!_\"m!+!\"!!m\"m!%!\"!!v\"m!&!\"!!~\"m!%!\"!\")\"m!&!\"!\"1\"m!)!\"!\">\"m!&!\"!\"M\"m!%!\"!\"V\"m!&!\"!\"^\"m!&!\"!\"j\"m!%!\"!\"s\"m!&!\"!\"~\"m!%!\"!%2\"m!&!\"!%<\"m!%!\"!%E\"m!&!\"!%Q\"m!%!\"!%[\"m!+!\"!%i\"m!%!\"!!J\"n!&!\"!!U\"n!%!\"!!]\"n!-!\"!!m\"n!%!\"!!v\"n!&!\"!!~\"n!%!\"!\")\"n!&!\"!\"2\"n!*!\"!\">\"n!&!\"!\"M\"n!%!\"!\"V\"n!&!\"!\"^\"n!%!\"!\"j\"n!%!\"!\"s\"n!&!\"!\"~\"n!%!\"!%2\"n!0!\"!%E\"n!%!\"!%Q\"n!%!\"!%Y\"n!-!\"!%i\"n!%!\"!!J\"o!&!\"!!U\"o!&!\"!!\\\"o!.!\"!!m\"o!%!\"!!v\"o!&!\"!!~\"o!%!\"!\")\"o!&!\"!\"4\"o!)!\"!\">\"o!&!\"!\"M\"o!%!\"!\"V\"o!&!\"!\"^\"o!%!\"!\"j\"o!%!\"!\"s\"o!&!\"!\"~\"o!%!\"!%1\"o!1!\"!%E\"o!%!\"!%Q\"o!%!\"!%X\"o!.!\"!%i\"o!%!\"!!J\"p!&!\"!!T\"p!&!\"!!\\\"p!%!\"!!e\"p!%!\"!!m\"p!%!\"!!v\"p!&!\"!!~\"p!%!\"!\")\"p!&!\"!\"7\"p!&!\"!\">\"p!&!\"!\"M\"p!%!\"!\"V\"p!&!\"!\"^\"p!%!\"!\"j\"p!%!\"!\"s\"p!&!\"!\"~\"p!%!\"!%1\"p!1!\"!%E\"p!%!\"!%Q\"p!%!\"!%X\"p!%!\"!%a\"p!%!\"!%i\"p!%!\"!!J\"q!&!\"!"
  "!S\"q!'!\"!!\\\"q!%!\"!!d\"q!&!\"!!m\"q!%!\"!!v\"q!&!\"!!~\"q!%!\"!\")\"q!&!\"!\"8\"q!%!\"!\">\"q!&!\"!\"M\"q!%!\"!\"V\"q!&!\"!\"^\"q!%!\"!\"j\"q!%!\"!\"s\"q!&!\"!\"~\"q!&!\"!%1\"q!2!\"!%E\"q!%!\"!%Q\"q!%!\"!%X\"q!%!\"!%`\"q!&!\"!%i\"q!%!\"!!J\"r!0!\"!!\\\"r!%!\"!!d\"r!&!\"!!m\"r!%!\"!!v\"r!&!\"!!~\"r!%!\"!\")\"r!&!\"!\"1\"r!\"!\"!\"7\"r!&!\"!\"?\"r!'!\"!\"I\"r!\"!\"!\"M\"r!%!\"!\"V\"r!&!\"!\"^\"r!%!\"!\"j\"r!%!\"!\"s\"r!&!\"!\"~\"r!&!\"!%0\"r!&!\"!%=\"r!&!\"!%E\"r!%!\"!%Q\"r!%!\"!%X\"r!%!\"!%`\"r!&!\"!%i\"r!%!\"!!J\"s!/!\"!!\\\"s!.!\"!!m\"s!%!\"!!v\"s!&!\"!!~\"s!%!\"!\")\"s!&!\"!\"1\"s!+!\"!\"@\"s!+!\"!\"M\"s!%!\"!\"V\"s!&!\"!\"^\"s!%!\"!\"j\"s!%!\"!\"s\"s!&!\"!\"~\"s!)!\"!%0\"s!&!\"!%=\"s!&!\"!%E\"s!%!\"!%Q\"s!%!\"!%X\"s!.!\"!%i\"s!%!\"!!J\"t!.!\"!!]\"t!(!\"!!e\"t!%!\"!!m\"t!%!\"!!v\"t!&!\"!!~\"t!%!\"!\")\"t!&!\"!\"1\"t!+!\"!\"A\"t!*!\"!\"M\"t!%!\"!\"V\"t!&!\"!\"^\"t!%!\"!\"j\"t!%!\"!\"s\"t!&!\"!#!\"t!(!\"!%0\"t!&!\"!%>\"t!&!\"!%E\"t!%!\"!%Q\"t!%!\"!%Y\"t!(!\"!%a\"t!%!\"!%i\"t!%!\"!!J\"u!,!\"!!^\"u!&!\"!!e\"u!%!\"!!m\"u!%!\"!!v\"u!&!\"!!~\"u!%!\"!\")\"u!&!\"!\"2\"u!(!\"!\"C\"u!'!\"!\"M\"u!%!\"!\"V\"u!&!\"!\"^\"u!%!\"!\"j\"u!%!\"!"
  "\"s\"u!&!\"!#\"\"u!'!\"!%/\"u!&!\"!%>\"u!&!\"!%E\"u!%!\"!%Q\"u!%!\"!%Z\"u!&!\"!%a\"u!%!\"!%i\"u!%!\"!!M#-!'!\"!!Z#-!*!\"!!o#-!'!\"!!}#-!'!\"!\",#-!*!\"!\"?#-!'!\"!\"K#-!%!\"!\"U#-!%!\"!\"[#-!&!\"!\"f#-!$!\"!#!#-!(!\"!#/#-!,!\"!#?#-!$!\"!%4#-!'!\"!%A#-!*!\"!%V#-!'!\"!%d#-!'!\"!%t#-!'!\"!&##-!*!\"!&3#-!-!\"!&F#-!'!\"!&T#-!$!\"!&v#-!(!\"!'&#-!,!\"!'6#-!$!\"!!M#.!'!\"!!Z#.!,!\"!!m#.!*!\"!!{#.!*!\"!\",#.!,!\"!\"?#.!'!\"!\"K#.!%!\"!\"U#.!%!\"!\"[#.!&!\"!\"f#.!$!\"!\"}#.!+!\"!#/#.!,!\"!#?#.!$!\"!%4#.!'!\"!%A#.!,!\"!%T#.!*!\"!%b#.!*!\"!%t#.!'!\"!&##.!,!\"!&3#.!-!\"!&F#.!'!\"!&T#.!$!\"!&t#.!+!\"!'&#.!,!\"!'6#.!$!\"!!M#/!'!\"!!Z#/!-!\"!!l#/!+!\"!!z#/!,!\"!\",#/!-!\"!\"?#/!'!\"!\"K#/!%!\"!\"U#/!%!\"!\"[#/!'!\"!\"f#/!$!\"!\"|#/!,!\"!#/#/!,!\"!#?#/!$!\"!%4#/!'!\"!%A#/!-!\"!%S#/!+!\"!%a#/!,!\"!%t#/!'!\"!&##/!,!\"!&3#/!-!\"!&F#/!'!\"!&T#/!$!\"!&s#/!,!\"!'&#/!,!\"!'6#/!$!\"!!M#0!'!\"!!Z#0!$!\"!!a#0!&!\"!!k#0!&!\"!!u#0!\"!\"!!z#0!$!\"!\"##0!%!\"!\",#0!$!\"!\"3#0!&!\"!\"?#0!'!\"!\"K#0!%!\"!\"U#0!%!\"!\"[#0!'!\"!\"f#0!$!\"!\"|#0!%!\"!#(#0!\"!\"!"
  "#/#0!$!\"!#?#0!$!\"!%4#0!'!\"!%A#0!$!\"!%H#0!&!\"!%R#0!&!\"!%\\#0!\"!\"!%a#0!$!\"!%h#0!%!\"!%t#0!'!\"!&##0!$!\"!&*#0!&!\"!&7#0!%!\"!&F#0!'!\"!&T#0!$!\"!&s#0!%!\"!&}#0!\"!\"!'&#0!$!\"!'6#0!$!\"!!L#1!$!\"!!P#1!%!\"!!Z#1!$!\"!!b#1!%!\"!!j#1!%!\"!!{#1!\"!\"!\"$#1!%!\"!\",#1!$!\"!\"4#1!%!\"!\">#1!$!\"!\"B#1!%!\"!\"K#1!%!\"!\"U#1!%!\"!\"[#1!(!\"!\"f#1!$!\"!\"|#1!$!\"!#/#1!$!\"!#?#1!$!\"!%3#1!$!\"!%7#1!%!\"!%A#1!$!\"!%I#1!%!\"!%Q#1!%!\"!%b#1!\"!\"!%i#1!%!\"!%s#1!$!\"!%w#1!%!\"!&##1!$!\"!&+#1!%!\"!&7#1!%!\"!&E#1!$!\"!&I#1!%!\"!&T#1!$!\"!&s#1!$!\"!'&#1!$!\"!'6#1!$!\"!!L#2!$!\"!!Q#2!$!\"!!Z#2!$!\"!!b#2!%!\"!!j#2!%!\"!\"$#2!%!\"!\",#2!$!\"!\"4#2!%!\"!\">#2!$!\"!\"C#2!$!\"!\"K#2!%!\"!\"U#2!%!\"!\"[#2!(!\"!\"f#2!$!\"!\"|#2!$!\"!#/#2!$!\"!#?#2!$!\"!%3#2!$!\"!%8#2!$!\"!%A#2!$!\"!%I#2!%!\"!%Q#2!%!\"!%i#2!%!\"!%s#2!$!\"!%x#2!$!\"!&##2!$!\"!&+#2!%!\"!&7#2!%!\"!&E#2!$!\"!&J#2!$!\"!&T#2!$!\"!&s#2!$!\"!'&#2!$!\"!'6#2!$!\"!!L#3!$!\"!!Q#3!$!\"!!Z#3!$!\"!!b#3!%!\"!!j#3!$!\"!\"$#3!%!\"!\",#3!$!\"!\"4#3!%!\"!\">#3!$!\"!\"C#3!$!\"!\"K#3!%!\"!"
  "\"U#3!%!\"!\"[#3!(!\"!\"f#3!$!\"!\"|#3!%!\"!#/#3!$!\"!#?#3!$!\"!%3#3!$!\"!%8#3!$!\"!%A#3!$!\"!%I#3!%!\"!%Q#3!$!\"!%i#3!%!\"!%s#3!$!\"!%x#3!$!\"!&##3!$!\"!&+#3!%!\"!&7#3!%!\"!&E#3!$!\"!&J#3!$!\"!&T#3!$!\"!&s#3!%!\"!'&#3!$!\"!'6#3!$!\"!!K#4!%!\"!!Q#4!%!\"!!Z#4!$!\"!!a#4!%!\"!!i#4!%!\"!\"$#4!$!\"!\",#4!$!\"!\"3#4!%!\"!\"=#4!%!\"!\"C#4!%!\"!\"K#4!%!\"!\"U#4!%!\"!\"[#4!)!\"!\"f#4!$!\"!\"|#4!(!\"!#/#4!$!\"!#?#4!$!\"!%2#4!%!\"!%8#4!%!\"!%A#4!$!\"!%H#4!%!\"!%P#4!%!\"!%i#4!$!\"!%r#4!%!\"!%x#4!%!\"!&##4!$!\"!&*#4!%!\"!&7#4!%!\"!&D#4!%!\"!&J#4!%!\"!&T#4!$!\"!&s#4!(!\"!'&#4!$!\"!'6#4!$!\"!!K#5!$!\"!!Q#5!%!\"!!Z#5!+!\"!!i#5!%!\"!\"##5!%!\"!\",#5!+!\"!\"=#5!$!\"!\"C#5!%!\"!\"K#5!/!\"!\"[#5!%!\"!\"`#5!$!\"!\"f#5!$!\"!\"}#5!)!\"!#/#5!+!\"!#?#5!$!\"!%2#5!$!\"!%8#5!%!\"!%A#5!+!\"!%P#5!%!\"!%h#5!%!\"!%r#5!$!\"!%x#5!%!\"!&##5!,!\"!&7#5!%!\"!&D#5!$!\"!&J#5!%!\"!&T#5!$!\"!&t#5!)!\"!'&#5!+!\"!'6#5!$!\"!!K#6!$!\"!!R#6!$!\"!!Z#6!+!\"!!i#6!%!\"!\"\"#6!%!\"!\",#6!+!\"!\"=#6!$!\"!\"D#6!$!\"!\"K#6!/!\"!\"[#6!%!\"!\"`#6!%!\"!\"f#6!$!\"!\"~#6!*!\"!"
  "#/#6!+!\"!#?#6!$!\"!%2#6!$!\"!%9#6!$!\"!%A#6!+!\"!%P#6!%!\"!%g#6!%!\"!%r#6!$!\"!%y#6!$!\"!&##6!*!\"!&7#6!%!\"!&D#6!$!\"!&K#6!$!\"!&T#6!$!\"!&u#6!*!\"!'&#6!+!\"!'6#6!$!\"!!K#7!$!\"!!R#7!$!\"!!Z#7!-!\"!!i#7!%!\"!\"\"#7!%!\"!\",#7!-!\"!\"=#7!$!\"!\"D#7!$!\"!\"K#7!/!\"!\"[#7!%!\"!\"a#7!$!\"!\"f#7!$!\"!#\"#7!)!\"!#/#7!+!\"!#?#7!$!\"!%2#7!$!\"!%9#7!$!\"!%A#7!-!\"!%P#7!%!\"!%g#7!%!\"!%r#7!$!\"!%y#7!$!\"!&##7!*!\"!&7#7!%!\"!&D#7!$!\"!&K#7!$!\"!&T#7!$!\"!&w#7!)!\"!'&#7!+!\"!'6#7!$!\"!!J#8!%!\"!!R#8!%!\"!!Z#8!$!\"!!b#8!%!\"!!i#8!%!\"!\"!#8!%!\"!\",#8!$!\"!\"4#8!%!\"!\"<#8!%!\"!\"D#8!%!\"!\"K#8!%!\"!\"U#8!%!\"!\"[#8!%!\"!\"a#8!%!\"!\"f#8!$!\"!#%#8!&!\"!#/#8!$!\"!#?#8!$!\"!%1#8!%!\"!%9#8!%!\"!%A#8!$!\"!%I#8!%!\"!%P#8!%!\"!%f#8!%!\"!%q#8!%!\"!%y#8!%!\"!&##8!$!\"!&)#8!%!\"!&7#8!%!\"!&C#8!%!\"!&K#8!%!\"!&T#8!$!\"!&z#8!&!\"!'&#8!$!\"!'6#8!$!\"!!J#9!-!\"!!Z#9!$!\"!!c#9!%!\"!!i#9!%!\"!!~#9!%!\"!\",#9!$!\"!\"5#9!%!\"!\"<#9!-!\"!\"K#9!%!\"!\"U#9!%!\"!\"[#9!%!\"!\"b#9!(!\"!#&#9!%!\"!#/#9!$!\"!#?#9!$!\"!%1#9!-!\"!%A#9!$!\"!%J#9!%!\"!"
  "%P#9!%!\"!%e#9!%!\"!%q#9!-!\"!&##9!$!\"!&)#9!%!\"!&7#9!%!\"!&C#9!-!\"!&T#9!$!\"!&{#9!%!\"!'&#9!$!\"!'6#9!$!\"!!J#:!-!\"!!Z#:!$!\"!!c#:!%!\"!!j#:!%!\"!!}#:!%!\"!\",#:!$!\"!\"5#:!%!\"!\"<#:!-!\"!\"K#:!%!\"!\"U#:!%!\"!\"[#:!%!\"!\"b#:!(!\"!#'#:!$!\"!#/#:!$!\"!#?#:!$!\"!%1#:!-!\"!%A#:!$!\"!%J#:!%!\"!%Q#:!%!\"!%d#:!%!\"!%q#:!-!\"!&##:!$!\"!&*#:!%!\"!&7#:!%!\"!&C#:!-!\"!&T#:!$!\"!&|#:!$!\"!'&#:!$!\"!'6#:!$!\"!!I#;!/!\"!!Z#;!$!\"!!c#;!%!\"!!j#;!%!\"!!|#;!%!\"!\",#;!$!\"!\"5#;!%!\"!\";#;!/!\"!\"K#;!%!\"!\"U#;!%!\"!\"[#;!%!\"!\"c#;!'!\"!#&#;!%!\"!#/#;!$!\"!#?#;!$!\"!%0#;!/!\"!%A#;!$!\"!%J#;!%!\"!%Q#;!%!\"!%c#;!%!\"!%p#;!/!\"!&##;!$!\"!&*#;!%!\"!&7#;!%!\"!&B#;!/!\"!&T#;!$!\"!&{#;!%!\"!'&#;!$!\"!'6#;!$!\"!!I#<!$!\"!!S#<!%!\"!!Z#<!$!\"!!b#<!%!\"!!j#<!'!\"!!u#<!\"!\"!!{#<!%!\"!\",#<!$!\"!\"4#<!%!\"!\";#<!$!\"!\"E#<!%!\"!\"K#<!%!\"!\"U#<!%!\"!\"[#<!%!\"!\"c#<!'!\"!\"{#<!#!\"!#%#<!&!\"!#/#<!$!\"!#?#<!$!\"!%0#<!$!\"!%:#<!%!\"!%A#<!$!\"!%I#<!%!\"!%Q#<!'!\"!%\\#<!\"!\"!%b#<!%!\"!%p#<!$!\"!%z#<!%!\"!&##<!$!\"!&+#<!%!\"!&7#<!%!\"!"
  "&B#<!$!\"!&L#<!%!\"!&T#<!$!\"!&r#<!#!\"!&z#<!&!\"!'&#<!$!\"!'6#<!$!\"!!I#=!$!\"!!S#=!%!\"!!Z#=!-!\"!!k#=!,!\"!!z#=!-!\"!\",#=!-!\"!\";#=!$!\"!\"E#=!%!\"!\"K#=!%!\"!\"U#=!%!\"!\"[#=!%!\"!\"c#=!'!\"!\"{#=!-!\"!#/#=!,!\"!#?#=!,!\"!%0#=!$!\"!%:#=!%!\"!%A#=!-!\"!%R#=!,!\"!%a#=!-!\"!%p#=!$!\"!%z#=!%!\"!&##=!$!\"!&+#=!%!\"!&3#=!-!\"!&B#=!$!\"!&L#=!%!\"!&T#=!,!\"!&r#=!-!\"!'&#=!,!\"!'6#=!,!\"!!H#>!%!\"!!T#>!$!\"!!Z#>!,!\"!!l#>!+!\"!!z#>!-!\"!\",#>!,!\"!\":#>!%!\"!\"F#>!$!\"!\"K#>!%!\"!\"U#>!%!\"!\"[#>!%!\"!\"d#>!&!\"!\"{#>!,!\"!#/#>!,!\"!#?#>!,!\"!%/#>!%!\"!%;#>!$!\"!%A#>!,!\"!%S#>!+!\"!%a#>!-!\"!%o#>!%!\"!%{#>!$!\"!&##>!$!\"!&,#>!%!\"!&3#>!-!\"!&A#>!%!\"!&M#>!$!\"!&T#>!,!\"!&r#>!,!\"!'&#>!,!\"!'6#>!,!\"!!H#?!%!\"!!T#?!%!\"!!Z#?!*!\"!!n#?!(!\"!!z#?!-!\"!\",#?!*!\"!\":#?!%!\"!\"F#?!%!\"!\"K#?!%!\"!\"U#?!%!\"!\"[#?!%!\"!\"d#?!&!\"!\"}#?!(!\"!#/#?!,!\"!#?#?!,!\"!%/#?!%!\"!%;#?!%!\"!%A#?!*!\"!%U#?!(!\"!%a#?!-!\"!%o#?!%!\"!%{#?!%!\"!&##?!$!\"!&,#?!%!\"!&3#?!-!\"!&A#?!%!\"!&M#?!%!\"!&T#?!,!\"!&t#?!(!\"!'&#?!,!\"!'6#?!,!\"!"
  "\"j#C!1!\"!&a#C!1!\"!\"j#D!1!\"!&a#D!1!\"!\"j#E!1!\"!&a#E!1!\"!!P#v!#!\"!$G#v!$!\"!$}#v!#!\"!!O#w!#!\"!\"'#w!#!\"!\"A#w!$!\"!\"r#w!#!\"!#Z#w!#!\"!#f#w!#!\"!$5#w!#!\"!$I#w!\"!\"!$~#w!#!\"!%a#w!#!\"!%k#w!#!\"!&$#w!#!\"!!9#x!)!\"!!J#x!$!\"!!O#x!\"!\"!\"&#x!#!\"!\"@#x!%!\"!\"r#x!#!\"!#2#x!\"!\"!#7#x!#!\"!#Z#x!#!\"!#e#x!#!\"!$5#x!#!\"!$I#x!\"!\"!%!#x!\"!\"!%$#x!$!\"!%/#x!#!\"!%a#x!#!\"!%j#x!%!\"!&$#x!#!\"!!9#y!*!\"!!J#y!\"!\"!!L#y!#!\"!\"&#y!\"!\"!\"0#y!#!\"!\"?#y!#!\"!\"d#y!#!\"!\"r#y!#!\"!#3#y!\"!\"!#7#y!\"!\"!#V#y!(!\"!#e#y!\"!\"!$5#y!#!\"!$H#y!#!\"!%##y!#!\"!%&#y!#!\"!%/#y!#!\"!%M#y!(!\"!%a#y!#!\"!%i#y!#!\"!%m#y!\"!\"!&$#y!#!\"!!9#z!#!\"!!@#z!$!\"!!I#z!\"!\"!!M#z!#!\"!\"%#z!\"!\"!\"0#z!#!\"!\"?#z!#!\"!\"d#z!#!\"!\"r#z!#!\"!#4#z!$!\"!#V#z!(!\"!#d#z!#!\"!$5#z!#!\"!$H#z!\"!\"!%\"#z!#!\"!%'#z!\"!\"!%M#z!\"!\"!%P#z!$!\"!%a#z!#!\"!%i#z!\"!\"!%m#z!#!\"!&$#z!#!\"!!9#{!#!\"!!A#{!#!\"!\"0#{!#!\"!\"?#{!#!\"!\"d#{!#!\"!\"r#{!#!\"!#Z#{!#!\"!$5#{!#!\"!%a#{!#!\"!&$#{!#!\"!!9#|!#!\"!!A#|!#!\"!\"0#|!#!\"!\"?#|!#!\"!\"d#|!#!\"!\"r#|!#!\"!#Z#|!#!\"!"
  "$5#|!#!\"!%a#|!#!\"!&$#|!#!\"!!9#}!#!\"!!A#}!#!\"!!I#}!&!\"!!T#}!#!\"!!X#}!%!\"!!`#}!%!\"!!q#}!#!\"!!u#}!%!\"!\"!#}!#!\"!\")#}!#!\"!\".#}!(!\"!\"=#}!(!\"!\"I#}!&!\"!\"U#}!#!\"!\"Y#}!%!\"!\"b#}!(!\"!\"r#}!#!\"!\"v#}!%!\"!#%#}!&!\"!#3#}!&!\"!#A#}!&!\"!#T#}!%!\"!#Z#}!#!\"!#c#}!&!\"!#o#}!#!\"!#s#}!%!\"!$\"#}!%!\"!$(#}!#!\"!$5#}!#!\"!$9#}!&!\"!$F#}!&!\"!$Q#}!#!\"!$U#}!%!\"!$b#}!%!\"!$h#}!#!\"!$u#}!#!\"!$y#}!#!\"!%##}!&!\"!%/#}!#!\"!%?#}!%!\"!%E#}!#!\"!%N#}!&!\"!%a#}!#!\"!%j#}!%!\"!%t#}!#!\"!%x#}!%!\"!&$#}!#!\"!&(#}!%!\"!!9#~!#!\"!!A#~!#!\"!!G#~!)!\"!!T#~!#!\"!!W#~!'!\"!!^#~!(!\"!!q#~!#!\"!!t#~!'!\"!\"!#~!#!\"!\")#~!#!\"!\".#~!(!\"!\"=#~!'!\"!\"G#~!*!\"!\"U#~!#!\"!\"X#~!'!\"!\"b#~!(!\"!\"r#~!#!\"!\"u#~!'!\"!###~!*!\"!#1#~!)!\"!#?#~!)!\"!#R#~!+!\"!#a#~!*!\"!#o#~!#!\"!#r#~!'!\"!#~#~!+!\"!$5#~!#!\"!$8#~!(!\"!$D#~!)!\"!$Q#~!#!\"!$T#~!'!\"!$`#~!+!\"!$u#~!#!\"!$x#~!%!\"!%!#~!*!\"!%/#~!#!\"!%=#~!+!\"!%L#~!*!\"!%a#~!#!\"!%h#~!)!\"!%t#~!#!\"!%w#~!'!\"!&$#~!#!\"!&'#~!'!\"!!9$!!#!\"!!?$!!$!\"!!G$!!\"!\"!!M$!!$!\"!!T$!!$!\"!"
  "![$!!%!\"!!c$!!#!\"!!q$!!$!\"!!x$!!$!\"!\"!$!!#!\"!\")$!!#!\"!\"0$!!#!\"!\"?$!!#!\"!\"F$!!$!\"!\"N$!!#!\"!\"U$!!$!\"!\"\\$!!$!\"!\"d$!!#!\"!\"r$!!$!\"!\"y$!!$!\"!#\"$!!$!\"!#*$!!#!\"!#1$!!\"!\"!#7$!!$!\"!#>$!!$!\"!#Q$!!$!\"!#X$!!%!\"!#`$!!$!\"!#h$!!#!\"!#o$!!$!\"!#v$!!$!\"!#}$!!$!\"!$&$!!%!\"!$5$!!%!\"!$=$!!$!\"!$D$!!\"!\"!$J$!!$!\"!$Q$!!$!\"!$X$!!$!\"!$_$!!$!\"!$f$!!%!\"!$u$!!%!\"!$~$!!$!\"!%($!!#!\"!%/$!!#!\"!%<$!!$!\"!%C$!!%!\"!%K$!!$!\"!%S$!!#!\"!%a$!!#!\"!%g$!!$!\"!%n$!!#!\"!%t$!!$!\"!%{$!!$!\"!&$$!!$!\"!&+$!!$!\"!!9$\"!)!\"!!N$\"!#!\"!!T$\"!$!\"!!\\$\"!#!\"!!d$\"!#!\"!!q$\"!$!\"!!y$\"!#!\"!\"!$\"!#!\"!\")$\"!#!\"!\"0$\"!#!\"!\"?$\"!#!\"!\"F$\"!#!\"!\"O$\"!#!\"!\"U$\"!$!\"!\"]$\"!#!\"!\"d$\"!#!\"!\"r$\"!$!\"!\"z$\"!#!\"!#\"$\"!#!\"!#+$\"!#!\"!#8$\"!#!\"!#>$\"!#!\"!#Q$\"!#!\"!#Y$\"!$!\"!#`$\"!#!\"!#i$\"!#!\"!#o$\"!$!\"!#w$\"!#!\"!#}$\"!#!\"!$'$\"!$!\"!$5$\"!$!\"!$>$\"!#!\"!$K$\"!#!\"!$Q$\"!$!\"!$Y$\"!#!\"!$_$\"!#!\"!$g$\"!$!\"!$u$\"!$!\"!$~$\"!#!\"!%)$\"!#!\"!%/$\"!#!\"!%<$\"!#!\"!%D$\"!$!\"!%K$\"!#!\"!%T$\"!#!\"!%a$\"!#!\"!%g$\"!#!\"!%o$\"!#!\"!%t$\"!$!\"!"
  "%|$\"!#!\"!&$$\"!$!\"!&,$\"!#!\"!!9$#!*!\"!!N$#!#!\"!!T$#!#!\"!!\\$#!#!\"!!d$#!#!\"!!q$#!#!\"!!y$#!#!\"!\"!$#!#!\"!\")$#!#!\"!\"0$#!#!\"!\"?$#!#!\"!\"E$#!$!\"!\"O$#!#!\"!\"U$#!#!\"!\"]$#!#!\"!\"d$#!#!\"!\"r$#!#!\"!\"z$#!#!\"!#!$#!$!\"!#+$#!#!\"!#8$#!#!\"!#=$#!$!\"!#P$#!$!\"!#Z$#!#!\"!#_$#!$!\"!#i$#!#!\"!#o$#!#!\"!#w$#!#!\"!#|$#!$!\"!$($#!#!\"!$5$#!#!\"!$>$#!#!\"!$K$#!#!\"!$Q$#!#!\"!$Y$#!#!\"!$^$#!$!\"!$h$#!#!\"!$u$#!#!\"!$}$#!$!\"!%)$#!#!\"!%/$#!#!\"!%;$#!$!\"!%E$#!#!\"!%J$#!$!\"!%T$#!#!\"!%a$#!#!\"!%f$#!#!\"!%o$#!#!\"!%t$#!#!\"!%|$#!#!\"!&$$#!#!\"!&,$#!#!\"!!9$$!#!\"!!A$$!#!\"!!J$$!'!\"!!T$$!#!\"!!\\$$!#!\"!!d$$!#!\"!!q$$!#!\"!!y$$!#!\"!\"!$$!#!\"!\")$$!#!\"!\"0$$!#!\"!\"?$$!#!\"!\"E$$!#!\"!\"O$$!#!\"!\"U$$!#!\"!\"]$$!#!\"!\"d$$!#!\"!\"r$$!#!\"!\"z$$!#!\"!#!$$!#!\"!#+$$!#!\"!#4$$!'!\"!#=$$!#!\"!#P$$!#!\"!#Z$$!#!\"!#_$$!#!\"!#i$$!#!\"!#o$$!#!\"!#w$$!#!\"!#|$$!#!\"!$($$!#!\"!$5$$!#!\"!$>$$!#!\"!$G$$!'!\"!$Q$$!#!\"!$Y$$!#!\"!$^$$!#!\"!$h$$!#!\"!$u$$!#!\"!$}$$!#!\"!%)$$!#!\"!%/$$!#!\"!%;$$!#!\"!%E$$!#!\"!%J$$!#!\"!"
  "%T$$!#!\"!%a$$!#!\"!%f$$!,!\"!%t$$!#!\"!%|$$!#!\"!&$$$!#!\"!&,$$!#!\"!!9$%!#!\"!!B$%!#!\"!!H$%!)!\"!!T$%!#!\"!!\\$%!#!\"!!d$%!#!\"!!q$%!#!\"!!y$%!#!\"!\"!$%!#!\"!\")$%!#!\"!\"0$%!#!\"!\"?$%!#!\"!\"E$%!#!\"!\"O$%!#!\"!\"U$%!#!\"!\"]$%!#!\"!\"d$%!#!\"!\"r$%!#!\"!\"z$%!#!\"!#!$%!#!\"!#+$%!#!\"!#2$%!)!\"!#=$%!#!\"!#P$%!#!\"!#Z$%!#!\"!#_$%!#!\"!#i$%!#!\"!#o$%!#!\"!#w$%!#!\"!#|$%!#!\"!$($%!#!\"!$5$%!#!\"!$>$%!$!\"!$E$%!)!\"!$Q$%!#!\"!$Y$%!#!\"!$^$%!#!\"!$h$%!#!\"!$u$%!#!\"!$}$%!#!\"!%)$%!#!\"!%/$%!#!\"!%;$%!#!\"!%E$%!#!\"!%J$%!#!\"!%T$%!#!\"!%a$%!#!\"!%f$%!,!\"!%t$%!#!\"!%|$%!#!\"!&$$%!#!\"!&,$%!#!\"!!9$&!#!\"!!B$&!#!\"!!G$&!#!\"!!N$&!#!\"!!T$&!#!\"!!\\$&!#!\"!!d$&!#!\"!!q$&!#!\"!!y$&!#!\"!\"!$&!#!\"!\")$&!#!\"!\"0$&!#!\"!\"?$&!#!\"!\"E$&!#!\"!\"O$&!#!\"!\"U$&!#!\"!\"]$&!#!\"!\"d$&!#!\"!\"r$&!#!\"!\"z$&!#!\"!#!$&!#!\"!#+$&!#!\"!#1$&!#!\"!#8$&!#!\"!#=$&!#!\"!#P$&!#!\"!#Z$&!#!\"!#_$&!#!\"!#i$&!#!\"!#o$&!#!\"!#w$&!#!\"!#|$&!#!\"!$($&!#!\"!$5$&!#!\"!$>$&!#!\"!$D$&!#!\"!$K$&!#!\"!$Q$&!#!\"!$Y$&!#!\"!$^$&!#!\"!$h$&!#!\"!"
  "$u$&!#!\"!$}$&!#!\"!%)$&!#!\"!%/$&!#!\"!%;$&!#!\"!%E$&!#!\"!%J$&!#!\"!%T$&!#!\"!%a$&!#!\"!%f$&!#!\"!%t$&!#!\"!%|$&!#!\"!&$$&!#!\"!&,$&!#!\"!!9$'!#!\"!!B$'!#!\"!!F$'!#!\"!!N$'!#!\"!!T$'!#!\"!!\\$'!#!\"!!d$'!#!\"!!q$'!#!\"!!y$'!#!\"!\"!$'!#!\"!\")$'!#!\"!\"0$'!#!\"!\"?$'!#!\"!\"E$'!$!\"!\"O$'!#!\"!\"U$'!#!\"!\"]$'!#!\"!\"d$'!#!\"!\"r$'!#!\"!\"z$'!#!\"!#!$'!$!\"!#+$'!#!\"!#0$'!#!\"!#8$'!#!\"!#=$'!$!\"!#P$'!#!\"!#Y$'!$!\"!#_$'!$!\"!#i$'!#!\"!#o$'!#!\"!#w$'!#!\"!#|$'!#!\"!$'$'!$!\"!$5$'!#!\"!$>$'!#!\"!$C$'!#!\"!$K$'!#!\"!$Q$'!#!\"!$Y$'!#!\"!$^$'!#!\"!$g$'!$!\"!$u$'!#!\"!$}$'!$!\"!%)$'!#!\"!%/$'!#!\"!%;$'!#!\"!%D$'!$!\"!%J$'!$!\"!%T$'!#!\"!%a$'!#!\"!%f$'!$!\"!%t$'!#!\"!%|$'!#!\"!&$$'!#!\"!&,$'!#!\"!!9$(!#!\"!!A$(!$!\"!!F$(!#!\"!!N$(!#!\"!!T$(!#!\"!!\\$(!#!\"!!d$(!#!\"!!q$(!#!\"!!y$(!#!\"!\"!$(!#!\"!\")$(!#!\"!\"0$(!#!\"!\"?$(!#!\"!\"F$(!#!\"!\"O$(!#!\"!\"U$(!#!\"!\"]$(!#!\"!\"d$(!#!\"!\"r$(!#!\"!\"z$(!#!\"!#\"$(!#!\"!#+$(!#!\"!#0$(!#!\"!#8$(!#!\"!#>$(!#!\"!#Q$(!#!\"!#Y$(!$!\"!#`$(!#!\"!#i$(!#!\"!#o$(!#!\"!#w$(!#!\"!"
  "#}$(!#!\"!$'$(!$!\"!$5$(!$!\"!$>$(!#!\"!$C$(!#!\"!$K$(!#!\"!$Q$(!#!\"!$Y$(!#!\"!$_$(!#!\"!$g$(!$!\"!$u$(!#!\"!$~$(!#!\"!%)$(!#!\"!%/$(!#!\"!%<$(!#!\"!%D$(!$!\"!%K$(!#!\"!%T$(!#!\"!%a$(!#!\"!%g$(!#!\"!%t$(!#!\"!%|$(!#!\"!&$$(!#!\"!&,$(!#!\"!!9$)!#!\"!!A$)!#!\"!!F$)!$!\"!!M$)!$!\"!!T$)!#!\"!!\\$)!#!\"!!d$)!#!\"!!q$)!#!\"!!y$)!#!\"!\"!$)!$!\"!\"($)!$!\"!\"0$)!#!\"!\"?$)!#!\"!\"F$)!$!\"!\"N$)!#!\"!\"U$)!#!\"!\"]$)!#!\"!\"d$)!#!\"!\"r$)!#!\"!\"z$)!#!\"!#\"$)!$!\"!#*$)!#!\"!#0$)!$!\"!#7$)!$!\"!#>$)!$!\"!#Q$)!$!\"!#X$)!%!\"!#`$)!$!\"!#h$)!#!\"!#o$)!#!\"!#w$)!#!\"!#}$)!$!\"!$&$)!%!\"!$5$)!$!\"!$=$)!#!\"!$C$)!$!\"!$J$)!$!\"!$Q$)!#!\"!$Y$)!#!\"!$_$)!$!\"!$f$)!%!\"!$u$)!#!\"!$~$)!$!\"!%($)!#!\"!%/$)!#!\"!%<$)!$!\"!%C$)!%!\"!%K$)!$!\"!%S$)!#!\"!%a$)!#!\"!%g$)!$!\"!%o$)!\"!\"!%t$)!#!\"!%|$)!#!\"!&$$)!#!\"!&,$)!#!\"!&2$)!#!\"!!9$*!+!\"!!G$*!'!\"!!N$*!#!\"!!T$*!#!\"!!\\$*!#!\"!!d$*!#!\"!!q$*!#!\"!!y$*!#!\"!\"\"$*!*!\"!\"0$*!&!\"!\"?$*!#!\"!\"G$*!)!\"!\"U$*!#!\"!\"]$*!#!\"!\"d$*!&!\"!\"r$*!#!\"!\"z$*!#!\"!##$*!)!\"!#1$*!'!\"!"
  "#8$*!#!\"!#?$*!)!\"!#R$*!(!\"!#Z$*!#!\"!#a$*!)!\"!#o$*!#!\"!#w$*!#!\"!#~$*!(!\"!$($*!#!\"!$5$*!#!\"!$8$*!(!\"!$D$*!'!\"!$K$*!#!\"!$Q$*!#!\"!$Y$*!#!\"!$`$*!(!\"!$h$*!#!\"!$u$*!#!\"!%!$*!)!\"!%/$*!#!\"!%=$*!(!\"!%E$*!#!\"!%L$*!)!\"!%a$*!#!\"!%h$*!)!\"!%t$*!#!\"!%|$*!#!\"!&$$*!#!\"!&,$*!#!\"!&1$*!$!\"!!9$+!)!\"!!H$+!%!\"!!N$+!#!\"!!T$+!#!\"!!\\$+!#!\"!!d$+!#!\"!!q$+!#!\"!!y$+!#!\"!\"#$+!%!\"!\")$+!#!\"!\"1$+!%!\"!\"?$+!#!\"!\"I$+!&!\"!\"U$+!#!\"!\"]$+!#!\"!\"e$+!%!\"!\"r$+!#!\"!\"z$+!#!\"!#%$+!&!\"!#2$+!%!\"!#8$+!#!\"!#A$+!&!\"!#S$+!&!\"!#Z$+!#!\"!#c$+!&!\"!#o$+!#!\"!#w$+!#!\"!$!$+!&!\"!$($+!#!\"!$5$+!#!\"!$9$+!%!\"!$E$+!%!\"!$K$+!#!\"!$Q$+!#!\"!$Y$+!#!\"!$a$+!&!\"!$h$+!#!\"!$u$+!#!\"!%#$+!&!\"!%/$+!#!\"!%>$+!&!\"!%E$+!#!\"!%N$+!&!\"!%a$+!#!\"!%i$+!'!\"!%t$+!#!\"!%|$+!#!\"!&$$+!#!\"!&,$+!#!\"!&2$+!#!\"!$($,!#!\"!$h$,!#!\"!%E$,!#!\"!#5$-!\"!\"!$'$-!#!\"!$g$-!#!\"!%D$-!#!\"!%k$-!#!\"!#5$.!\"!\"!$'$.!#!\"!$g$.!#!\"!%D$.!#!\"!%k$.!#!\"!#}$/!\"!\"!$&$/!$!\"!$_$/!\"!\"!$f$/!$!\"!%<$/!\"!\"!%C$/!$!\"!#}$0!*!\"!"
  "$_$0!*!\"!%<$0!*!\"!#~$1!'!\"!$`$1!'!\"!%=$1!'!\"!\";$@!#!\"!\"y$@!#!\"!!>$A!#!\"!\";$A!#!\"!\"y$A!#!\"!#W$A!*!\"!!=$B!%!\"!!X$B!#!\"!\";$B!#!\"!\"y$B!#!\"!#+$B!(!\"!#4$B!#!\"!#W$B!*!\"!#s$B!#!\"!!=$C!%!\"!!X$C!#!\"!\";$C!#!\"!\"y$C!#!\"!#*$C!\"!\"!#.$C!$!\"!#4$C!#!\"!#W$C!#!\"!#s$C!#!\"!!=$D!%!\"!!X$D!#!\"!\";$D!#!\"!\"y$D!#!\"!#4$D!#!\"!#W$D!#!\"!#s$D!#!\"!!<$E!#!\"!!@$E!#!\"!!X$E!#!\"!!v$E!\"!\"!\";$E!#!\"!\"y$E!#!\"!#4$E!\"!\"!#@$E!\"!\"!#W$E!#!\"!#s$E!#!\"!!<$F!#!\"!!@$F!#!\"!!I$F!#!\"!!Q$F!#!\"!!V$F!(!\"!!b$F!&!\"!!v$F!$!\"!\"2$F!&!\"!\";$F!#!\"!\"?$F!%!\"!\"L$F!&!\"!\"X$F!#!\"!\"\\$F!%!\"!\"p$F!&!\"!\"y$F!#!\"!\"}$F!%!\"!#)$F!#!\"!#1$F!$!\"!#@$F!$!\"!#W$F!#!\"!#d$F!#!\"!#h$F!%!\"!#q$F!(!\"!#}$F!%!\"!$)$F!#!\"!$-$F!#!\"!!<$G!#!\"!!@$G!$!\"!!I$G!#!\"!!Q$G!#!\"!!V$G!(!\"!!`$G!*!\"!!w$G!%!\"!\"0$G!)!\"!\";$G!#!\"!\">$G!'!\"!\"J$G!*!\"!\"X$G!#!\"!\"[$G!'!\"!\"n$G!)!\"!\"y$G!#!\"!\"|$G!'!\"!#)$G!#!\"!#1$G!#!\"!#A$G!%!\"!#W$G!#!\"!#d$G!#!\"!#g$G!'!\"!#q$G!(!\"!#{$G!)!\"!$)$G!#!\"!$,$G!%!\"!!;$H!#!\"!!A$H!#!\"!!I$H!#!\"!"
  "!Q$H!#!\"!!X$H!#!\"!!_$H!$!\"!!g$H!#!\"!!y$H!%!\"!\"/$H!$!\"!\";$H!$!\"!\"B$H!$!\"!\"I$H!$!\"!\"Q$H!#!\"!\"X$H!$!\"!\"_$H!$!\"!\"m$H!$!\"!\"y$H!$!\"!#\"$H!$!\"!#)$H!#!\"!#1$H!#!\"!#C$H!%!\"!#W$H!#!\"!#d$H!$!\"!#k$H!$!\"!#s$H!#!\"!#z$H!$!\"!$#$H!#!\"!$)$H!%!\"!!;$I!#!\"!!A$I!#!\"!!I$I!#!\"!!Q$I!#!\"!!X$I!#!\"!!_$I!#!\"!!h$I!#!\"!!{$I!$!\"!\"/$I!#!\"!\";$I!$!\"!\"C$I!#!\"!\"I$I!#!\"!\"R$I!#!\"!\"X$I!$!\"!\"`$I!#!\"!\"m$I!#!\"!\"y$I!$!\"!##$I!#!\"!#)$I!#!\"!#1$I!#!\"!#E$I!$!\"!#W$I!*!\"!#d$I!$!\"!#l$I!#!\"!#s$I!#!\"!#z$I!#!\"!$$$I!#!\"!$)$I!$!\"!!:$J!$!\"!!B$J!#!\"!!I$J!#!\"!!Q$J!#!\"!!X$J!#!\"!!^$J!$!\"!!h$J!#!\"!!}$J!$!\"!\".$J!$!\"!\";$J!#!\"!\"C$J!#!\"!\"H$J!$!\"!\"R$J!#!\"!\"X$J!#!\"!\"`$J!#!\"!\"l$J!$!\"!\"y$J!#!\"!##$J!#!\"!#)$J!#!\"!#1$J!#!\"!#G$J!$!\"!#W$J!*!\"!#d$J!#!\"!#l$J!#!\"!#s$J!#!\"!#y$J!#!\"!$$$J!#!\"!$)$J!#!\"!!:$K!#!\"!!B$K!#!\"!!I$K!#!\"!!Q$K!#!\"!!X$K!#!\"!!^$K!#!\"!!h$K!#!\"!!~$K!$!\"!\".$K!#!\"!\";$K!#!\"!\"C$K!#!\"!\"H$K!#!\"!\"R$K!#!\"!\"X$K!#!\"!\"`$K!#!\"!\"l$K!#!\"!\"y$K!#!\"!##$K!#!\"!#)$K!#!\"!"
  "#1$K!#!\"!#H$K!$!\"!#W$K!#!\"!#d$K!#!\"!#l$K!#!\"!#s$K!#!\"!#y$K!,!\"!$)$K!#!\"!!:$L!+!\"!!I$L!#!\"!!Q$L!#!\"!!X$L!#!\"!!^$L!#!\"!!h$L!#!\"!!|$L!$!\"!\".$L!#!\"!\";$L!#!\"!\"C$L!#!\"!\"H$L!#!\"!\"R$L!#!\"!\"X$L!#!\"!\"`$L!#!\"!\"l$L!#!\"!\"y$L!#!\"!##$L!#!\"!#)$L!#!\"!#1$L!#!\"!#F$L!$!\"!#W$L!#!\"!#d$L!#!\"!#l$L!#!\"!#s$L!#!\"!#y$L!,!\"!$)$L!#!\"!!9$M!-!\"!!I$M!#!\"!!Q$M!#!\"!!X$M!#!\"!!^$M!#!\"!!h$M!#!\"!!z$M!$!\"!\".$M!#!\"!\";$M!#!\"!\"C$M!#!\"!\"H$M!#!\"!\"R$M!#!\"!\"X$M!#!\"!\"`$M!#!\"!\"l$M!#!\"!\"y$M!#!\"!##$M!#!\"!#)$M!#!\"!#1$M!#!\"!#D$M!$!\"!#W$M!#!\"!#d$M!#!\"!#l$M!#!\"!#s$M!#!\"!#y$M!#!\"!$)$M!#!\"!!9$N!#!\"!!C$N!#!\"!!I$N!#!\"!!Q$N!#!\"!!X$N!#!\"!!^$N!$!\"!!h$N!#!\"!!x$N!$!\"!\".$N!$!\"!\";$N!#!\"!\"C$N!#!\"!\"H$N!$!\"!\"R$N!#!\"!\"X$N!#!\"!\"`$N!#!\"!\"l$N!$!\"!\"y$N!#!\"!##$N!#!\"!#)$N!#!\"!#1$N!#!\"!#B$N!$!\"!#W$N!#!\"!#d$N!#!\"!#l$N!#!\"!#s$N!#!\"!#y$N!$!\"!$)$N!#!\"!!9$O!#!\"!!C$O!$!\"!!I$O!#!\"!!Q$O!#!\"!!X$O!#!\"!!_$O!#!\"!!h$O!#!\"!!v$O!$!\"!\"/$O!#!\"!\";$O!#!\"!\"C$O!#!\"!\"I$O!#!\"!\"R$O!#!\"!"
  "\"X$O!#!\"!\"`$O!#!\"!\"m$O!#!\"!\"y$O!#!\"!##$O!#!\"!#)$O!#!\"!#0$O!$!\"!#@$O!$!\"!#W$O!#!\"!#d$O!#!\"!#l$O!#!\"!#s$O!#!\"!#z$O!#!\"!$)$O!#!\"!!8$P!#!\"!!D$P!#!\"!!I$P!$!\"!!P$P!$!\"!!X$P!#!\"!!_$P!$!\"!!g$P!#!\"!!v$P!\"!\"!\"/$P!$!\"!\";$P!#!\"!\"C$P!#!\"!\"I$P!$!\"!\"Q$P!#!\"!\"X$P!#!\"!\"`$P!#!\"!\"m$P!$!\"!\"y$P!#!\"!##$P!#!\"!#)$P!$!\"!#/$P!%!\"!#@$P!\"!\"!#W$P!#!\"!#d$P!#!\"!#l$P!#!\"!#s$P!#!\"!#z$P!$!\"!$$$P!\"!\"!$)$P!#!\"!$2$P!#!\"!!8$Q!#!\"!!D$Q!#!\"!!J$Q!*!\"!!X$Q!&!\"!!`$Q!)!\"!\"0$Q!)!\"!\";$Q!#!\"!\"C$Q!#!\"!\"J$Q!)!\"!\"X$Q!#!\"!\"`$Q!#!\"!\"n$Q!)!\"!\"y$Q!#!\"!##$Q!#!\"!#*$Q!'!\"!#1$Q!#!\"!#W$Q!*!\"!#d$Q!#!\"!#l$Q!#!\"!#s$Q!&!\"!#{$Q!)!\"!$)$Q!#!\"!$1$Q!$!\"!!7$R!$!\"!!D$R!$!\"!!K$R!%!\"!!Q$R!#!\"!!Y$R!%!\"!!b$R!&!\"!\"2$R!&!\"!\";$R!#!\"!\"C$R!#!\"!\"L$R!&!\"!\"X$R!#!\"!\"`$R!#!\"!\"p$R!&!\"!\"y$R!#!\"!##$R!#!\"!#+$R!%!\"!#1$R!#!\"!#W$R!+!\"!#d$R!#!\"!#l$R!#!\"!#t$R!%!\"!#|$R!'!\"!$)$R!#!\"!$2$R!#!\"!\"M$T!#!\"!\"M$U!#!\"!&3$q!\"!\"!&<$q!#!\"!&?$q!\"!\"!&[$q!\"!\"!&o$q!#!\"!&x$q!#!\"!'%$q!)!\"!'2$q!#!\"!"
  "':$q!\"!\"!'=$q!\"!\"!'E$q!\"!\"!'H$q!+!\"!'V$q!#!\"!'\\$q!#!\"!&3$r!\"!\"!&;$r!\"!\"!&?$r!\"!\"!&[$r!\"!\"!&o$r!$!\"!&x$r!#!\"!'($r!#!\"!'2$r!$!\"!':$r!\"!\"!'=$r!\"!\"!'K$r!#!\"!'Q$r!\"!\"!'V$r!#!\"!'\\$r!\"!\"!&3$s!\"!\"!&;$s!\"!\"!&?$s!\"!\"!&[$s!\"!\"!&o$s!$!\"!&w$s!$!\"!'($s!#!\"!'2$s!$!\"!':$s!\"!\"!'=$s!\"!\"!'K$s!#!\"!'Q$s!\"!\"!'V$s!#!\"!'\\$s!\"!\"!%w$t!#!\"!%|$t!\"!\"!&\"$t!#!\"!&'$t!$!\"!&.$t!\"!\"!&0$t!#!\"!&3$t!\"!\"!&7$t!(!\"!&?$t!\"!\"!&E$t!$!\"!&K$t!#!\"!&P$t!\"!\"!&T$t!#!\"!&[$t!\"!\"!&]$t!%!\"!&c$t!#!\"!&i$t!\"!\"!&o$t!$!\"!&w$t!\"!\"!&y$t!\"!\"!&|$t!\"!\"!&~$t!#!\"!'($t!#!\"!'2$t!#!\"!'5$t!\"!\"!':$t!\"!\"!'=$t!\"!\"!'?$t!$!\"!'E$t!\"!\"!'K$t!#!\"!'Q$t!\"!\"!'V$t!$!\"!'\\$t!\"!\"!%x$u!\"!\"!%|$u!\"!\"!&\"$u!\"!\"!&%$u!#!\"!&*$u!\"!\"!&.$u!#!\"!&3$u!\"!\"!&6$u!#!\"!&;$u!\"!\"!&?$u!\"!\"!&C$u!#!\"!&H$u!\"!\"!&L$u!\"!\"!&P$u!\"!\"!&T$u!\"!\"!&[$u!#!\"!&`$u!#!\"!&d$u!\"!\"!&h$u!#!\"!&o$u!#!\"!&r$u!\"!\"!&w$u!\"!\"!&y$u!\"!\"!&|$u!#!\"!'($u!#!\"!'2$u!#!\"!'5$u!#!\"!':$u!\"!\"!'=$u!#!\"!'B$u!\"!\"!'E$u!\"!\"!'K$u!#!\"!'Q$u!#!\"!"
  "'U$u!\"!\"!'X$u!\"!\"!'[$u!#!\"!%x$v!\"!\"!%{$v!$!\"!&\"$v!\"!\"!&%$v!\"!\"!&+$v!\"!\"!&.$v!#!\"!&3$v!\"!\"!&5$v!#!\"!&;$v!\"!\"!&?$v!\"!\"!&C$v!\"!\"!&I$v!\"!\"!&L$v!\"!\"!&O$v!$!\"!&T$v!\"!\"!&[$v!#!\"!&a$v!\"!\"!&d$v!\"!\"!&h$v!\"!\"!&o$v!#!\"!&r$v!\"!\"!&v$v!\"!\"!&y$v!\"!\"!&|$v!#!\"!'($v!#!\"!'2$v!#!\"!'6$v!\"!\"!':$v!\"!\"!'=$v!#!\"!'B$v!\"!\"!'E$v!\"!\"!'K$v!#!\"!'R$v!\"!\"!'U$v!\"!\"!'X$v!\"!\"!'[$v!\"!\"!%x$w!\"!\"!%{$w!\"!\"!%}$w!\"!\"!&\"$w!\"!\"!&%$w!\"!\"!&+$w!\"!\"!&.$w!\"!\"!&3$w!$!\"!&;$w!\"!\"!&?$w!\"!\"!&C$w!\"!\"!&I$w!\"!\"!&L$w!\"!\"!&O$w!\"!\"!&Q$w!\"!\"!&T$w!\"!\"!&[$w!\"!\"!&a$w!\"!\"!&d$w!#!\"!&h$w!\"!\"!&o$w!#!\"!&s$w!\"!\"!&v$w!\"!\"!&y$w!\"!\"!&|$w!\"!\"!'($w!#!\"!'2$w!#!\"!'7$w!\"!\"!':$w!\"!\"!'=$w!\"!\"!'B$w!\"!\"!'E$w!\"!\"!'K$w!#!\"!'R$w!\"!\"!'U$w!\"!\"!'X$w!\"!\"!'[$w!\"!\"!%x$x!#!\"!%{$x!\"!\"!%}$x!\"!\"!&!$x!\"!\"!&%$x!\"!\"!&+$x!\"!\"!&.$x!\"!\"!&3$x!$!\"!&;$x!\"!\"!&?$x!\"!\"!&C$x!\"!\"!&I$x!\"!\"!&L$x!#!\"!&O$x!\"!\"!&Q$x!\"!\"!&S$x!\"!\"!&[$x!\"!\"!&a$x!\"!\"!&e$x!\"!\"!&g$x!#!\"!&o$x!#!\"!&s$x!\"!\"!&u$x!#!\"!&y$x!\"!\"!"
  "&|$x!\"!\"!'($x!#!\"!'2$x!#!\"!'7$x!#!\"!':$x!\"!\"!'=$x!\"!\"!'B$x!\"!\"!'E$x!\"!\"!'K$x!#!\"!'R$x!%!\"!'X$x!#!\"!'[$x!\"!\"!%y$y!#!\"!%}$y!$!\"!&%$y!\"!\"!&+$y!\"!\"!&.$y!\"!\"!&3$y!\"!\"!&6$y!\"!\"!&;$y!\"!\"!&?$y!\"!\"!&C$y!\"!\"!&I$y!\"!\"!&M$y!#!\"!&Q$y!$!\"!&[$y!#!\"!&a$y!\"!\"!&e$y!\"!\"!&g$y!\"!\"!&o$y!#!\"!&s$y!$!\"!&y$y!\"!\"!&|$y!\"!\"!'($y!#!\"!'2$y!#!\"!'8$y!$!\"!'=$y!\"!\"!'B$y!\"!\"!'E$y!\"!\"!'K$y!#!\"!'S$y!#!\"!'Y$y!$!\"!%y$z!#!\"!%~$z!#!\"!&%$z!#!\"!&*$z!\"!\"!&.$z!\"!\"!&3$z!\"!\"!&6$z!#!\"!&;$z!\"!\"!&?$z!\"!\"!&C$z!#!\"!&H$z!\"!\"!&M$z!#!\"!&R$z!#!\"!&[$z!#!\"!&`$z!\"!\"!&e$z!$!\"!&o$z!#!\"!&t$z!#!\"!&y$z!\"!\"!&|$z!\"!\"!'#$z!\"!\"!'($z!#!\"!'2$z!#!\"!'8$z!$!\"!'=$z!\"!\"!'B$z!\"!\"!'E$z!\"!\"!'K$z!#!\"!'S$z!#!\"!'Y$z!#!\"!%y${!#!\"!%~${!#!\"!&&${!%!\"!&.${!\"!\"!&3${!\"!\"!&7${!#!\"!&;${!\"!\"!&?${!\"!\"!&D${!%!\"!&M${!#!\"!&R${!#!\"!&[${!\"!\"!&]${!$!\"!&f${!\"!\"!&o${!#!\"!&t${!\"!\"!&y${!\"!\"!&|${!\"!\"!'#${!\"!\"!'(${!#!\"!'2${!#!\"!'9${!#!\"!'=${!\"!\"!'B${!\"!\"!'E${!\"!\"!'K${!#!\"!'S${!#!\"!'Y${!#!\"!"
  "&f$|!\"!\"!&e$}!#!\"!&c$~!$!\"!!!!!(_!\"\"!!!\"(_!\"\"!!!#(_!\"\"!!!$(_!\"\"!!!%(_!\"\"!7\"K$I!\"#$}\"K$I!\"#!7\"L!\"!\"#$^\"L!\"!\"#$}\"L!\"!\"#(F\"L!\"!\"#!7\"M!\"!\"#$^\"M!\"!\"#$}\"M!\"!\"#(F\"M!\"!\"#!7\"N!\"!\"#$^\"N!\"!\"#$}\"N!\"!\"#(F\"N!\"!\"#!7\"O!\"!\"#$^\"O!\"!\"#$}\"O!\"!\"#(F\"O!\"!\"#!7\"P!\"!\"#$^\"P!\"!\"#$}\"P!\"!\"#(F\"P!\"!\"#!7\"Q!\"!\"#$^\"Q!\"!\"#$}\"Q!\"!\"#(F\"Q!\"!\"#!7\"R!\"!\"#$^\"R!\"!\"#$}\"R!\"!\"#(F\"R!\"!\"#!7\"S!\"!\"#$^\"S!\"!\"#$}\"S!\"!\"#(F\"S!\"!\"#!7\"T!\"!\"#$^\"T!\"!\"#$}\"T!\"!\"#(F\"T!\"!\"#!7\"U!\"!\"#$^\"U!\"!\"#$}\"U!\"!\"#(F\"U!\"!\"#!7\"V!\"!\"#$^\"V!\"!\"#$}\"V!\"!\"#(F\"V!\"!\"#!7\"W!\"!\"#$^\"W!\"!\"#$}\"W!\"!\"#(F\"W!\"!\"#!7\"X!\"!\"#$^\"X!\"!\"#$}\"X!\"!\"#(F\"X!\"!\"#!7\"Y!\"!\"#$^\"Y!\"!\"#$}\"Y!\"!\"#(F\"Y!\"!\"#!7\"Z!\"!\"#$^\"Z!\"!\"#$}\"Z!\"!\"#(F\"Z!\"!\"#!7\"[!\"!\"#$^\"[!\"!\"#$}\"[!\"!\"#(F\"[!\"!\"#!7\"\\!\"!\"#$^\"\\!\"!\"#$}\"\\!\"!\"#(F\"\\!\"!\"#!7\"]!\"!\"#$^\"]!\"!\"#$}\"]!\"!\"#(F\"]!\"!\"#!7\"^!\"!\"#$^\"^!\"!\"#$}\"^!\"!\"#(F\"^!\"!\"#!7\"_!\"!\"#$^\"_!\"!\"#$}\"_!\"!\"#(F\"_!\"!\"#!7\"`!\"!\"#$^\"`!\"!\"#$}\"`!\"!\"#(F\"`!\"!\"#!7\"a!\"!\"#$^\"a!\"!\"#$}\"a!\"!\"#(F\"a!\"!\"#!7\"b!\"!\"#$^\"b!\"!\"#"
  "$}\"b!\"!\"#(F\"b!\"!\"#!7\"c!\"!\"#$^\"c!\"!\"#$}\"c!\"!\"#(F\"c!\"!\"#!7\"d!\"!\"#$^\"d!\"!\"#$}\"d!\"!\"#(F\"d!\"!\"#!7\"e!\"!\"#$^\"e!\"!\"#$}\"e!\"!\"#(F\"e!\"!\"#!7\"f!\"!\"#$^\"f!\"!\"#$}\"f!\"!\"#(F\"f!\"!\"#!7\"g!\"!\"#$^\"g!\"!\"#$}\"g!\"!\"#(F\"g!\"!\"#!7\"h!\"!\"#$^\"h!\"!\"#$}\"h!\"!\"#(F\"h!\"!\"#!7\"i!\"!\"#$^\"i!\"!\"#$}\"i!\"!\"#(F\"i!\"!\"#!7\"j!\"!\"#$^\"j!\"!\"#$}\"j!\"!\"#(F\"j!\"!\"#!7\"k!\"!\"#$^\"k!\"!\"#$}\"k!\"!\"#(F\"k!\"!\"#!7\"l!\"!\"#$^\"l!\"!\"#$}\"l!\"!\"#(F\"l!\"!\"#!7\"m!\"!\"#$^\"m!\"!\"#$}\"m!\"!\"#(F\"m!\"!\"#!7\"n!\"!\"#$^\"n!\"!\"#$}\"n!\"!\"#(F\"n!\"!\"#!7\"o!\"!\"#$^\"o!\"!\"#$}\"o!\"!\"#(F\"o!\"!\"#!7\"p!\"!\"#$^\"p!\"!\"#$}\"p!\"!\"#(F\"p!\"!\"#!7\"q!\"!\"#$^\"q!\"!\"#$}\"q!\"!\"#(F\"q!\"!\"#!7\"r!\"!\"#$^\"r!\"!\"#$}\"r!\"!\"#(F\"r!\"!\"#!7\"s!\"!\"#$^\"s!\"!\"#$}\"s!\"!\"#(F\"s!\"!\"#!7\"t!\"!\"#$^\"t!\"!\"#$}\"t!\"!\"#(F\"t!\"!\"#!7\"u!\"!\"#$^\"u!\"!\"#$}\"u!\"!\"#(F\"u!\"!\"#!7\"v!\"!\"#$^\"v!\"!\"#$}\"v!\"!\"#(F\"v!\"!\"#!7\"w!\"!\"#$^\"w!\"!\"#$}\"w!\"!\"#(F\"w!\"!\"#!7\"x!\"!\"#$^\"x!\"!\"#$}\"x!\"!\"#(F\"x!\"!\"#!7\"y!\"!\"#$^\"y!\"!\"#$}\"y!\"!\"#(F\"y!\"!\"#!7\"z!\"!\"#$^\"z!\"!\"#$}\"z!\"!\"#(F\"z!\"!\"#!7\"{!\"!\"#$^\"{!\"!\"#"
  "$}\"{!\"!\"#(F\"{!\"!\"#!7\"|!\"!\"#$^\"|!\"!\"#$}\"|!\"!\"#(F\"|!\"!\"#!7\"}!\"!\"#$^\"}!\"!\"#$}\"}!\"!\"#(F\"}!\"!\"#!7\"~!\"!\"#$^\"~!\"!\"#$}\"~!\"!\"#(F\"~!\"!\"#!7#!!\"!\"#$^#!!\"!\"#$}#!!\"!\"#(F#!!\"!\"#!7#\"!\"!\"#$^#\"!\"!\"#$}#\"!\"!\"#(F#\"!\"!\"#!7##!\"!\"#$^##!\"!\"#$}##!\"!\"#(F##!\"!\"#!7#$!\"!\"#$^#$!\"!\"#$}#$!\"!\"#(F#$!\"!\"#!7#%!\"!\"#$^#%!\"!\"#$}#%!\"!\"#(F#%!\"!\"#!7#&!\"!\"#$^#&!\"!\"#$}#&!\"!\"#(F#&!\"!\"#!7#'!\"!\"#$^#'!\"!\"#$}#'!\"!\"#(F#'!\"!\"#!7#(!\"!\"#$^#(!\"!\"#$}#(!\"!\"#(F#(!\"!\"#!7#)!\"!\"#$^#)!\"!\"#$}#)!\"!\"#(F#)!\"!\"#!7#*!\"!\"#$^#*!\"!\"#$}#*!\"!\"#(F#*!\"!\"#!7#+!\"!\"#$^#+!\"!\"#$}#+!\"!\"#(F#+!\"!\"#!7#,!\"!\"#$^#,!\"!\"#$}#,!\"!\"#(F#,!\"!\"#!7#-!\"!\"#$^#-!\"!\"#$}#-!\"!\"#(F#-!\"!\"#!7#.!\"!\"#$^#.!\"!\"#$}#.!\"!\"#(F#.!\"!\"#!7#/!\"!\"#$^#/!\"!\"#$}#/!\"!\"#(F#/!\"!\"#!7#0!\"!\"#$^#0!\"!\"#$}#0!\"!\"#(F#0!\"!\"#!7#1!\"!\"#$^#1!\"!\"#$}#1!\"!\"#(F#1!\"!\"#!7#2!\"!\"#$^#2!\"!\"#$}#2!\"!\"#(F#2!\"!\"#!7#3!\"!\"#$^#3!\"!\"#$}#3!\"!\"#(F#3!\"!\"#!7#4!\"!\"#$^#4!\"!\"#$}#4!\"!\"#(F#4!\"!\"#!7#5!\"!\"#$^#5!\"!\"#$}#5!\"!\"#(F#5!\"!\"#!7#6!\"!\"#$^#6!\"!\"#"
  "$}#6!\"!\"#(F#6!\"!\"#!7#7!\"!\"#$^#7!\"!\"#$}#7!\"!\"#(F#7!\"!\"#!7#8!\"!\"#$^#8!\"!\"#$}#8!\"!\"#(F#8!\"!\"#!7#9!\"!\"#$^#9!\"!\"#$}#9!\"!\"#(F#9!\"!\"#!7#:!\"!\"#$^#:!\"!\"#$}#:!\"!\"#(F#:!\"!\"#!7#;!\"!\"#$^#;!\"!\"#$}#;!\"!\"#(F#;!\"!\"#!7#<!\"!\"#$^#<!\"!\"#$}#<!\"!\"#(F#<!\"!\"#!7#=!\"!\"#$^#=!\"!\"#$}#=!\"!\"#(F#=!\"!\"#!7#>!\"!\"#$^#>!\"!\"#$}#>!\"!\"#(F#>!\"!\"#!7#?!\"!\"#$^#?!\"!\"#$}#?!\"!\"#(F#?!\"!\"#!7#@!\"!\"#$^#@!\"!\"#$}#@!\"!\"#(F#@!\"!\"#!7#A!\"!\"#$^#A!\"!\"#$}#A!\"!\"#(F#A!\"!\"#!7#B!\"!\"#$^#B!\"!\"#$}#B!\"!\"#(F#B!\"!\"#!7#C!\"!\"#$^#C!\"!\"#$}#C!\"!\"#(F#C!\"!\"#!7#D!\"!\"#$^#D!\"!\"#$}#D!\"!\"#(F#D!\"!\"#!7#E!\"!\"#$^#E!\"!\"#$}#E!\"!\"#(F#E!\"!\"#!7#F!\"!\"#$^#F!\"!\"#$}#F!\"!\"#(F#F!\"!\"#!7#G!\"!\"#$^#G!\"!\"#$}#G!\"!\"#(F#G!\"!\"#!7#H!\"!\"#$^#H!\"!\"#$}#H!\"!\"#(F#H!\"!\"#!7#I!\"!\"#$^#I!\"!\"#$}#I!\"!\"#(F#I!\"!\"#!7#J!\"!\"#$^#J!\"!\"#$}#J!\"!\"#(F#J!\"!\"#!7#K!\"!\"#$^#K!\"!\"#$}#K!\"!\"#(F#K!\"!\"#!7#L!\"!\"#$^#L!\"!\"#$}#L!\"!\"#(F#L!\"!\"#!7#M!\"!\"#$^#M!\"!\"#$}#M!\"!\"#(F#M!\"!\"#!7#N!\"!\"#$^#N!\"!\"#$}#N!\"!\"#(F#N!\"!\"#!7#O!\"!\"#$^#O!\"!\"#"
  "$}#O!\"!\"#(F#O!\"!\"#!7#P!\"!\"#$^#P!\"!\"#$}#P!\"!\"#(F#P!\"!\"#!7#Q!\"!\"#$^#Q!\"!\"#$}#Q!\"!\"#(F#Q!\"!\"#!7#R!\"!\"#$^#R!\"!\"#$}#R!\"!\"#(F#R!\"!\"#!7#S!\"!\"#$^#S!\"!\"#$}#S!\"!\"#(F#S!\"!\"#!7#T!\"!\"#$^#T!\"!\"#$}#T!\"!\"#(F#T!\"!\"#!7#U!\"!\"#$^#U!\"!\"#$}#U!\"!\"#(F#U!\"!\"#!7#V!\"!\"#$^#V!\"!\"#$}#V!\"!\"#(F#V!\"!\"#!7#W!\"!\"#$^#W!\"!\"#$}#W!\"!\"#(F#W!\"!\"#!7#X!\"!\"#$^#X!\"!\"#$}#X!\"!\"#(F#X!\"!\"#!7#Y$I!\"#$}#Y$I!\"#"
 ))
))

(defun A2FLT:NoticePair (s i)
  (+ (* 94 (- (ascii (substr s i 1)) 33))
     (- (ascii (substr s (1+ i) 1)) 33)))
(defun A2FLT:DrawNotice (/ w h asset hit ox oy data i)
  (setq w (1+ (dimx_tile "notice")) h (1+ (dimy_tile "notice")))
  (foreach asset *A2FLT-NoticeAssets*
    (if (and (<= (car asset) w) (<= (cadr asset) h)) (setq hit asset)))
  (if hit
    (progn
      (setq ox (fix (/ (- w (car hit)) 2.0))
            oy (fix (/ (- h (cadr hit)) 2.0)) data (caddr hit) i 1)
      (start_image "notice")
      (fill_image 0 0 w h -15)
      (while (<= (+ i 8) (strlen data))
        (fill_image (+ ox (A2FLT:NoticePair data i))
          (+ oy (A2FLT:NoticePair data (+ i 2)))
          (A2FLT:NoticePair data (+ i 4)) (A2FLT:NoticePair data (+ i 6))
          (nth (- (ascii (substr data (+ i 8) 1)) 33) '(-16 150 -18)))
        (setq i (+ i 9)))
      (end_image) T)
    nil))


;;; WPF window: same blue-gray language and button system as MAUNEN.
;;; In LT 2024 use STARTAPP, not unsupported third-party COM creation.
(setq *A2FLT-WpfScript* (list
  "param("
  "    [string]$ResultPath,"
  "    [string]$PreviewPath,"
  "    [switch]$InitialHelp,"
  "    [switch]$PreviewHelpPage2,"
  "    [string]$TestAction"
  ")"
  "$ErrorActionPreference = 'Stop'"
  "Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase"
  ""
  "$xaml = @'"
  "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""
  "        xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\""
  "        Title=\"ABC2FONT | V1.9.12\" Width=\"548\" Height=\"338\""
  "        WindowStartupLocation=\"CenterScreen\" ResizeMode=\"NoResize\""
  "        Background=\"#F4F6F8\" FontFamily=\"Segoe UI\" Topmost=\"True\">"
  "  <Window.Resources>"
  "    <Style x:Key=\"SecondaryButton\" TargetType=\"Button\">"
  "      <Setter Property=\"Height\" Value=\"38\"/>"
  "      <Setter Property=\"Padding\" Value=\"14,0\"/>"
  "      <Setter Property=\"FontSize\" Value=\"14\"/>"
  "      <Setter Property=\"FontWeight\" Value=\"SemiBold\"/>"
  "      <Setter Property=\"Foreground\" Value=\"#344550\"/>"
  "      <Setter Property=\"Background\" Value=\"White\"/>"
  "      <Setter Property=\"BorderBrush\" Value=\"#C5D0D7\"/>"
  "      <Setter Property=\"BorderThickness\" Value=\"1\"/>"
  "      <Setter Property=\"Cursor\" Value=\"Hand\"/>"
  "    </Style>"
  "    <Style x:Key=\"PrimaryButton\" TargetType=\"Button\" BasedOn=\"{StaticResource SecondaryButton}\">"
  "      <Setter Property=\"Foreground\" Value=\"White\"/>"
  "      <Setter Property=\"Background\" Value=\"#087FB5\"/>"
  "      <Setter Property=\"BorderBrush\" Value=\"#087FB5\"/>"
  "    </Style>"
  "  </Window.Resources>"
  "  <Grid Background=\"#F4F6F8\">"
  "    <Grid.RowDefinitions>"
  "      <RowDefinition Height=\"4\"/>"
  "      <RowDefinition Height=\"76\"/>"
  "      <RowDefinition Height=\"*\"/>"
  "      <RowDefinition Height=\"70\"/>"
  "    </Grid.RowDefinitions>"
  "    <Border Background=\"#0696D7\"/>"
  "    <Border Grid.Row=\"1\" Background=\"#242A30\" Padding=\"20,11\">"
  "      <StackPanel>"
  "        <TextBlock Text=\"ABC2FONT\" Foreground=\"White\" FontSize=\"24\" FontWeight=\"SemiBold\"/>"
  "        <TextBlock Text=\"TCVN3/ABC  →  Unicode\" Foreground=\"#77D0F4\" FontSize=\"14\" Margin=\"0,3,0,0\"/>"
  "      </StackPanel>"
  "    </Border>"
  "    <Grid Grid.Row=\"2\" Margin=\"20,14,20,10\">"
  "      <Grid Name=\"QuickPanel\">"
  "        <Grid.RowDefinitions>"
  "          <RowDefinition Height=\"Auto\"/>"
  "          <RowDefinition Height=\"15\"/>"
  "          <RowDefinition Height=\"Auto\"/>"
  "          <RowDefinition Height=\"*\"/>"
  "        </Grid.RowDefinitions>"
  "        <StackPanel>"
  "          <TextBlock Text=\"Chọn font đích\" FontSize=\"18\" FontWeight=\"SemiBold\" Foreground=\"#24323A\"/>"
  "          <TextBlock Text=\"Áp dụng cho chữ được chọn trong bản vẽ.\" FontSize=\"14\" Foreground=\"#52636D\" Margin=\"0,3,0,0\"/>"
  "        </StackPanel>"
  "        <Grid Grid.Row=\"2\">"
  "          <Grid.ColumnDefinitions>"
  "            <ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"12\"/><ColumnDefinition Width=\"*\"/>"
  "          </Grid.ColumnDefinitions>"
  "          <Button Name=\"BahnButton\" Grid.Column=\"0\" Content=\"BAHNSCHRIFT\" Style=\"{StaticResource PrimaryButton}\"/>"
  "          <Button Name=\"ArialButton\" Grid.Column=\"2\" Content=\"ARIAL\" Style=\"{StaticResource PrimaryButton}\"/>"
  "        </Grid>"
  "        <TextBlock Grid.Row=\"3\" VerticalAlignment=\"Bottom\" Text=\"Chọn font  →  Auto  →  chọn chữ  →  Enter.\""
  "                   FontSize=\"13\" Foreground=\"#52636D\"/>"
  "      </Grid>"
  "      <Grid Name=\"HelpPanel\" Visibility=\"Collapsed\">"
  "        <Grid.RowDefinitions><RowDefinition Height=\"Auto\"/><RowDefinition Height=\"*\"/></Grid.RowDefinitions>"
  "        <TextBlock Name=\"HelpTitle\" Text=\"01 / CHUẨN BỊ VÀ LỆNH\" FontSize=\"18\" FontWeight=\"SemiBold\" Foreground=\"#24323A\"/>"
  "        <TextBlock Name=\"HelpText\" Grid.Row=\"1\" Margin=\"0,12,0,0\" FontSize=\"13\" Foreground=\"#344550\""
  "                   TextWrapping=\"Wrap\" LineHeight=\"23\"/>"
  "      </Grid>"
  "    </Grid>"
  "    <Border Grid.Row=\"3\" Background=\"#E8EDF0\" BorderBrush=\"#D4DDE2\" BorderThickness=\"0,1,0,0\">"
  "      <Grid Margin=\"20,0,20,0\">"
  "        <Grid.ColumnDefinitions><ColumnDefinition Width=\"*\"/><ColumnDefinition Width=\"Auto\"/></Grid.ColumnDefinitions>"
  "        <Grid.RowDefinitions><RowDefinition Height=\"*\"/><RowDefinition Height=\"15\"/></Grid.RowDefinitions>"
  "        <StackPanel Grid.Row=\"0\" Orientation=\"Horizontal\" VerticalAlignment=\"Center\">"
  "          <Button Name=\"HelpButton\" Content=\"HƯỚNG DẪN\" Width=\"125\" Style=\"{StaticResource SecondaryButton}\"/>"
  "          <Button Name=\"BackButton\" Content=\"QUAY LẠI\" Width=\"110\" Style=\"{StaticResource SecondaryButton}\" Visibility=\"Collapsed\"/>"
  "          <Button Name=\"NextButton\" Content=\"TRANG TIẾP\" Width=\"115\" Margin=\"10,0,0,0\""
  "                  Style=\"{StaticResource SecondaryButton}\" Visibility=\"Collapsed\"/>"
  "        </StackPanel>"
  "        <Button Name=\"CloseButton\" Grid.Row=\"0\" Grid.Column=\"1\" Content=\"ĐÓNG\" Width=\"90\""
  "                Style=\"{StaticResource SecondaryButton}\" VerticalAlignment=\"Center\"/>"
  "        <TextBlock Grid.Row=\"1\" Grid.ColumnSpan=\"2\" Text=\"workflow by Mr.T NhiTW\""
  "                   HorizontalAlignment=\"Right\" VerticalAlignment=\"Top\" FontSize=\"10\" Foreground=\"#71808A\"/>"
  "      </Grid>"
  "    </Border>"
  "  </Grid>"
  "</Window>"
  "'@"
  ""
  "$window = [Windows.Markup.XamlReader]::Parse($xaml)"
  "$script:choice = 'CLOSE'"
  "$script:page = 0"
  "$quick = $window.FindName('QuickPanel')"
  "$help = $window.FindName('HelpPanel')"
  "$helpButton = $window.FindName('HelpButton')"
  "$backButton = $window.FindName('BackButton')"
  "$nextButton = $window.FindName('NextButton')"
  "$helpTitle = $window.FindName('HelpTitle')"
  "$helpText = $window.FindName('HelpText')"
  ""
  "function Set-HelpPage([int]$page) {"
  "    $script:page = $page"
  "    $window.Height = 470"
  "    $quick.Visibility = 'Collapsed'"
  "    $help.Visibility = 'Visible'"
  "    $helpButton.Visibility = 'Collapsed'"
  "    $backButton.Visibility = 'Visible'"
  "    $nextButton.Visibility = 'Visible'"
  "    if ($page -eq 1) {"
  "        $helpTitle.Text = '01 / CHUẨN BỊ VÀ LỆNH'"
  "        $helpText.Text = \"Lưu bản sao bản vẽ. Đặt LISPSYS = 1 rồi mở lại AutoCAD nếu vừa thay đổi giá trị.`n`nCHỌN CHỮ: ABC2BAHN_SEL (Bahnschrift), ABC2ARIAL_SEL (Arial).`nTOÀN BỘ BẢN VẼ: ABC2BAHN, ABC2ARIAL.`nFONT KHÁC: ABC2FONT_SEL hoặc ABC2FONT.`n`nABC2CHECK kiểm tra nhận dạng. UNDO hoàn tác.\""
  "        $nextButton.IsEnabled = $true"
  "    } else {"
  "        $helpTitle.Text = '02 / CHẾ ĐỘ VÀ PHẠM VI'"
  "        $helpText.Text = \"Auto tự nhận dạng TCVN3/ABC theo thông tin font.`nThường/Hoa chỉ dùng khi đã xác nhận chữ là ABC.`n`nLệnh _SEL xử lý chữ được chọn và thuộc tính; không sửa chữ trong định nghĩa block.`nLệnh toàn bộ bản vẽ xử lý thêm chữ trong định nghĩa block. Các bản chèn cùng định nghĩa có thể cùng đổi.`n`nBỏ qua Xref, layer khóa, TABLE, DIMENSION và MLEADER dạng block. Kiểm tra dấu và bố cục trước khi lưu.\""
  "        $nextButton.IsEnabled = $false"
  "    }"
  "}"
  "function Show-Quick {"
  "    $window.Height = 338"
  "    $quick.Visibility = 'Visible'"
  "    $help.Visibility = 'Collapsed'"
  "    $helpButton.Visibility = 'Visible'"
  "    $backButton.Visibility = 'Collapsed'"
  "    $nextButton.Visibility = 'Collapsed'"
  "    $script:page = 0"
  "}"
  ""
  "$window.FindName('BahnButton').Add_Click({ $script:choice = 'BAHN'; $window.Close() })"
  "$window.FindName('ArialButton').Add_Click({ $script:choice = 'ARIAL'; $window.Close() })"
  "$window.FindName('CloseButton').Add_Click({ $window.Close() })"
  "$helpButton.Add_Click({ Set-HelpPage 1 })"
  "$backButton.Add_Click({ if ($script:page -eq 2) { Set-HelpPage 1 } else { Show-Quick } })"
  "$nextButton.Add_Click({ Set-HelpPage 2 })"
  "if ($InitialHelp) { Set-HelpPage 1 }"
  "if ($PreviewHelpPage2) { Set-HelpPage 2 }"
  "if ($TestAction) {"
  "    $name = switch ($TestAction) {"
  "        'BAHN' { 'BahnButton' }"
  "        'ARIAL' { 'ArialButton' }"
  "        'HELP' { 'HelpButton' }"
  "        'NEXT' { 'NextButton' }"
  "        'CLOSE' { 'CloseButton' }"
  "        default { throw \"Unknown test action: $TestAction\" }"
  "    }"
  "    $window.FindName($name).RaiseEvent("
  "        [System.Windows.RoutedEventArgs]::new([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent))"
  "    if ($TestAction -eq 'HELP' -and $script:page -ne 1) { throw 'Help button did not open page 1' }"
  "    if ($TestAction -eq 'NEXT' -and $script:page -ne 2) { throw 'Next button did not open page 2' }"
  "    Write-Output \"choice=$script:choice page=$script:page\""
  "    exit"
  "}"
  ""
  "if ($PreviewPath) {"
  "    $root = $window.Content"
  "    $clientHeight = if ($InitialHelp -or $PreviewHelpPage2) { 431 } else { 299 }"
  "    $root.Measure([System.Windows.Size]::new(532, $clientHeight))"
  "    $root.Arrange([System.Windows.Rect]::new(0, 0, 532, $clientHeight))"
  "    $root.UpdateLayout()"
  "    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new("
  "        532, $clientHeight, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)"
  "    $bitmap.Render($root)"
  "    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()"
  "    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))"
  "    $stream = [System.IO.File]::Create($PreviewPath)"
  "    try { $encoder.Save($stream) } finally { $stream.Dispose() }"
  "} else {"
  "    if ($ResultPath) { [System.IO.File]::WriteAllText($ResultPath, 'READY', [System.Text.Encoding]::ASCII) }"
  "    try { [void]$window.ShowDialog() }"
  "    catch { $script:choice = 'ERROR' }"
  "    if ($ResultPath) { [System.IO.File]::WriteAllText($ResultPath, $script:choice, [System.Text.Encoding]::ASCII) }"
  "}"
))

(setq *A2FLT-FallbackDCL* (list
  "abc2_quick : dialog { label = \"ABC2FONT | V1.9.12\";"
  " : column {"
  " : text { label = \"TCVN3/ABC -> Unicode\"; }"
  " : text { label = \"Chon font dich cho chu duoc chon:\"; }"
  " : row {"
  " : button { key = \"bahn\"; label = \"Bahnschrift\"; width = 20; fixed_width = true; }"
  " : button { key = \"arial\"; label = \"Arial\"; width = 20; fixed_width = true; }"
  " }"
  " : text { label = \"Chon font -> Auto -> chon chu -> Enter.\"; }"
  " : row {"
  " : button { key = \"help\"; label = \"Huong dan\"; width = 16; fixed_width = true; }"
  " spacer;"
  " : button { key = \"close\"; label = \"Dong\"; width = 12; fixed_width = true; is_cancel = true; is_default = true; }"
  " }"
  " : text { label = \"workflow by Mr.T NhiTW\"; alignment = right; }"
  " }"
  "}"
))

(defun A2FLT:WriteLines (path lines / f line)
  (if (setq f (open path "w" "utf8-bom"))
    (progn (foreach line lines (write-line line f)) (close f) T)
    nil))

(defun A2FLT:ReadChoice (path / f s)
  (if (setq f (open path "r"))
    (progn (setq s (read-line f)) (close f) s)
    nil))

(defun A2FLT:RunWpf (helpMode / psPath resultPath args launched startMs phase choice paused)
  (setq psPath (vl-filename-mktemp "ABC2FONT_UI_" nil ".ps1")
        resultPath (vl-filename-mktemp "ABC2FONT_CHOICE_" nil ".txt"))
  (vl-file-delete resultPath)
  (if (not (A2FLT:WriteLines psPath *A2FLT-WpfScript*))
    (setq choice nil)
    (progn
      (setq args (strcat "-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy RemoteSigned -File \""
                         psPath "\" -ResultPath \"" resultPath "\""
                         (if helpMode " -InitialHelp" "")))
      (setq launched (vl-catch-all-apply 'startapp (list "powershell.exe" args)))
      (if (or (vl-catch-all-error-p launched) (null launched))
        (setq choice nil)
        (progn
          (setq startMs (getvar "MILLISECS") phase "START")
          (while (and (not choice) (/= phase "ERROR")
                      (< (A2FLT:ElapsedSeconds startMs (getvar "MILLISECS"))
                         (if (= phase "START") 15.0 1200.0)))
            (setq choice (A2FLT:ReadChoice resultPath))
            (if (equal choice "READY")
              (progn (setq choice nil)
                     (if (= phase "START")
                       (setq phase "WAIT" startMs (getvar "MILLISECS")))))
            (if (not choice)
              (progn
                (setq paused (vl-catch-all-apply 'command-s (list "_.DELAY" 100)))
                (if (vl-catch-all-error-p paused) (setq phase "ERROR")))))))))
  (if psPath (vl-file-delete psPath))
  (if resultPath (vl-file-delete resultPath))
  choice)

(defun A2FLT:RunFallback (/ path f id r)
  (setq path (vl-filename-mktemp "ABC2FONT_UI_" nil ".dcl"))
  (if (A2FLT:WriteLines path *A2FLT-FallbackDCL*)
    (progn
      (setq id (load_dialog path))
      (if (and id (> id 0) (new_dialog "abc2_quick" id))
        (progn
          (action_tile "bahn" "(done_dialog 2)")
          (action_tile "arial" "(done_dialog 3)")
          (action_tile "help" "(done_dialog 4)")
          (action_tile "close" "(done_dialog 0)")
          (setq r (start_dialog)))
        (setq r nil))
      (if (and id (> id 0)) (unload_dialog id))))
  (vl-file-delete path)
  r)

(defun A2FLT:ShowQuickPanel (/ r)
  (setq r (A2FLT:RunWpf nil))
  (if (or (null r) (equal r "ERROR"))
    (progn
      (prompt "\nABC2FONT: Cua so Windows khong mo duoc; chuyen sang bang CAD.")
      (setq r (A2FLT:RunFallback))))
  (cond
    ((or (equal r "BAHN") (equal r 2)) (c:ABC2BAHN_SEL))
    ((or (equal r "ARIAL") (equal r 3)) (c:ABC2ARIAL_SEL))
    ((equal r 4) (c:ABC2HELP)))
  (princ))

(defun c:ABC2HELP (/ r)
  (setq r (A2FLT:RunWpf T))
  (if (or (null r) (equal r "ERROR"))
    (alert
      (strcat "ABC2HELP | V1.9.12\n\n"
        "Luu ban sao ban ve. LISPSYS = 1; mo lai CAD sau khi doi.\n"
        "ABC2BAHN_SEL / ABC2ARIAL_SEL: chon chu, dung Auto.\n"
        "ABC2FONT_SEL: chon font khac; ABC2FONT: toan bo ban ve.\n"
        "ABC2CHECK: kiem tra nhan dang; UNDO: hoan tac.\n"
        "Bo qua Xref, layer khoa, TABLE, DIMENSION, MLEADER dang block.\n"
        "Kiem tra dau va bo cuc truoc khi luu.\n\n"
        "workflow by Mr.T NhiTW")))
  (princ))

(prompt "\nABC2FONT V1.9.12 | ABC2BAHN_SEL: Bahnschrift | ABC2ARIAL_SEL: Arial | ABC2HELP: chi tiet.")

(princ)

;;; Mr.T 2.0.0.4 command entry points; preserve the current implied selection.
(defun c:MRTFONT ()
  (setq *MRTFONT-Preselection* (ssget "_I" '((0 . "TEXT,MTEXT,ATTDEF,ATTRIB,MULTILEADER,INSERT"))))
  (c:ABC2FONT_SEL)
  (setq *MRTFONT-Preselection* nil) (princ))
(defun c:MRTFONTALL () (c:ABC2FONT) (princ))
(defun c:MRTFONTCHECK () (c:ABC2CHECK) (princ))
