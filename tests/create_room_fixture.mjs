import {Workbook,SpreadsheetFile} from '@oai/artifact-tool';
const book=Workbook.create();
const a=book.worksheets.add('Tên phòng');
a.getRange('A1:C5').values=[['Tên cũ','Tên mới','ID'],['PHÒNG KHÁM','PHÒNG ĐIỀU TRỊ','PHÒNG KHÁM'],['PHÒNG XÉT NGHIỆM','XÉT NGHIỆM A','PHÒNG XÉT NGHIỆM'],['PHÒNG XÉT NGHIỆM','XÉT NGHIỆM B','PHÒNG XÉT NGHIỆM'],['001','PHÒNG 1','001']];
const b=book.worksheets.add('Tầng 2');b.getRange('B3:C4').values=[['Tên cũ','Tên mới'],['PHÒNG HỒI SỨC','PHÒNG ĐIỀU TRỊ TÍCH CỰC']];
book.recalculate();
const out=await SpreadsheetFile.exportXlsx(book);await out.save('tests/room_fixture.xlsx');
