# -*- coding: utf-8 -*-

# Copyright (c) 2005 - 2010 Detlev Offenbach <detlev@die-offenbachs.de>
#

"""
Module implementing the Breakpoint viewer widget.
"""

from PyQt4.QtCore import *
from PyQt4.QtGui import *

from KdeQt.KQApplication import e4App

from EditBreakpointDialog import EditBreakpointDialog

class BreakPointViewer(QTreeView):
    """
    Class implementing the Breakpoint viewer widget.
    
    Breakpoints will be shown with all their details. They can be modified through
    the context menu of this widget.
    
    @signal sourceFile(string, int) emitted to show the source of a breakpoint
    """
    def __init__(self, parent = None):
        """
        Constructor
        
        @param parent the parent (QWidget)
        """
        QTreeView.__init__(self, parent)
        self.setObjectName("BreakPointViewer")
        
        self.__model = None
        
        self.setItemsExpandable(False)
        self.setRootIsDecorated(False)
        self.setAlternatingRowColors(True)
        self.setSelectionMode(QAbstractItemView.ExtendedSelection)
        self.setSelectionBehavior(QAbstractItemView.SelectRows)
        
        self.setWindowTitle(self.trUtf8("Breakpoints"))
        
        self.setContextMenuPolicy(Qt.CustomContextMenu)
        self.connect(self,SIGNAL('customContextMenuRequested(const QPoint &)'),
                     self.__showContextMenu)
        self.connect(self,SIGNAL('doubleClicked(const QModelIndex &)'),
                     self.__doubleClicked)
        
        self.__createPopupMenus()
        
        self.condHistory = QStringList()
        self.fnHistory = QStringList()
        self.fnHistory.append('')
        
    def setModel(self, model):
        """
        Public slot to set the breakpoint model.
        
        @param reference to the breakpoint model (BreakPointModel)
        """
        self.__model = model
        
        self.sortingModel = QSortFilterProxyModel()
        self.sortingModel.setSourceModel(self.__model)
        QTreeView.setModel(self, self.sortingModel)
        
        header = self.header()
        header.setSortIndicator(0, Qt.AscendingOrder)
        header.setSortIndicatorShown(True)
        header.setClickable(True)
        
        self.setSortingEnabled(True)
        
        self.__layoutDisplay()
        
    def __layoutDisplay(self):
        """
        Private slot to perform a layout operation.
        """
        self.doItemsLayout()
        self.__resizeColumns()
        self.__resort()
        
    def __resizeColumns(self):
        """
        Private slot to resize the view when items get added, edited or deleted.
        """
        self.header().resizeSections(QHeaderView.ResizeToContents)
        self.header().setStretchLastSection(True)
    
    def __resort(self):
        """
        Private slot to resort the tree.
        """
        self.model().sort(self.header().sortIndicatorSection(), 
                          self.header().sortIndicatorOrder())
        
    def __toSourceIndex(self, index):
        """
        Private slot to convert an index to a source index.
        
        @param index index to be converted (QModelIndex)
        """
        return self.sortingModel.mapToSource(index)
        
    def __fromSourceIndex(self, sindex):
        """
        Private slot to convert a source index to an index.
        
        @param sindex source index to be converted (QModelIndex)
        """
        return self.sortingModel.mapFromSource(sindex)
        
    def __setRowSelected(self, index, selected = True):
        """
        Private slot to select a complete row.
        
        @param index index determining the row to be selected (QModelIndex)
        @param selected flag indicating the action (bool)
        """
        if not index.isValid():
            return
        
        if selected:
            flags = QItemSelectionModel.SelectionFlags(\
                QItemSelectionModel.ClearAndSelect | QItemSelectionModel.Rows)
        else:
            flags = QItemSelectionModel.SelectionFlags(\
                QItemSelectionModel.Deselect | QItemSelectionModel.Rows)
        self.selectionModel().select(index, flags)
        
    def __createPopupMenus(self):
        """
        Private method to generate the popup menus.
        """
        self.menu = QMenu()
        self.menu.addAction(self.trUtf8("Add"), self.__addBreak)
        self.menu.addAction(self.trUtf8("Edit..."), self.__editBreak)
        self.menu.addSeparator()
        self.menu.addAction(self.trUtf8("Enable"), self.__enableBreak)
        self.menu.addAction(self.trUtf8("Enable all"), self.__enableAllBreaks)
        self.menu.addSeparator()
        self.menu.addAction(self.trUtf8("Disable"), self.__disableBreak)
        self.menu.addAction(self.trUtf8("Disable all"), self.__disableAllBreaks)
        self.menu.addSeparator()
        self.menu.addAction(self.trUtf8("Delete"), self.__deleteBreak)
        self.menu.addAction(self.trUtf8("Delete all"), self.__deleteAllBreaks)
        self.menu.addSeparator()
        self.menu.addAction(self.trUtf8("Goto"), self.__showSource)
        self.menu.addSeparator()
        self.menu.addAction(self.trUtf8("Configure..."), self.__configure)

        self.backMenuActions = {}
        self.backMenu = QMenu()
        self.backMenu.addAction(self.trUtf8("Add"), self.__addBreak)
        self.backMenuActions["EnableAll"] = \
            self.backMenu.addAction(self.trUtf8("Enable all"), self.__enableAllBreaks)
        self.backMenuActions["DisableAll"] = \
            self.backMenu.addAction(self.trUtf8("Disable all"), self.__disableAllBreaks)
        self.backMenuActions["DeleteAll"] = \
            self.backMenu.addAction(self.trUtf8("Delete all"), self.__deleteAllBreaks)
        self.connect(self.backMenu, SIGNAL('aboutToShow()'), self.__showBackMenu)
        self.backMenu.addSeparator()
        self.backMenu.addAction(self.trUtf8("Configure..."), self.__configure)

        self.multiMenu = QMenu()
        self.multiMenu.addAction(self.trUtf8("Add"), self.__addBreak)
        self.multiMenu.addSeparator()
        self.multiMenu.addAction(self.trUtf8("Enable selected"), 
            self.__enableSelectedBreaks)
        self.multiMenu.addAction(self.trUtf8("Enable all"), self.__enableAllBreaks)
        self.multiMenu.addSeparator()
        self.multiMenu.addAction(self.trUtf8("Disable selected"), 
            self.__disableSelectedBreaks)
        self.multiMenu.addAction(self.trUtf8("Disable all"), self.__disableAllBreaks)
        self.multiMenu.addSeparator()
        self.multiMenu.addAction(self.trUtf8("Delete selected"), 
            self.__deleteSelectedBreaks)
        self.multiMenu.addAction(self.trUtf8("Delete all"), self.__deleteAllBreaks)
        self.multiMenu.addSeparator()
        self.multiMenu.addAction(self.trUtf8("Configure..."), self.__configure)
    
    def __showContextMenu(self, coord):
        """
        Private slot to show the context menu.
        
        @param coord the position of the mouse pointer (QPoint)
        """
        cnt = self.__getSelectedItemsCount()
        if cnt <= 1:
            index = self.indexAt(coord)
            if index.isValid():
                cnt = 1
                self.__setRowSelected(index)
        coord = self.mapToGlobal(coord)
        if cnt > 1:
            self.multiMenu.popup(coord)
        elif cnt == 1:
            self.menu.popup(coord)
        else:
            self.backMenu.popup(coord)
    
    def __clearSelection(self):
        """
        Private slot to clear the selection.
        """
        for index in self.selectedIndexes():
            self.__setRowSelected(index, False)
            
    def __addBreak(self):
        """
        Private slot to handle the add breakpoint context menu entry.
        """
        dlg = EditBreakpointDialog((self.fnHistory[0], None), None,
            self.condHistory, self, modal = 1, addMode = 1,
            filenameHistory = self.fnHistory)
        if dlg.exec_() == QDialog.Accepted:
            fn, line, cond, temp, enabled, count = dlg.getAddData()
            if fn is not None:
                self.fnHistory.removeAll(fn)
                self.fnHistory.prepend(fn)
            
            if not cond.isEmpty():
                self.condHistory.removeAll(cond)
                self.condHistory.prepend(cond)
            
            self.__model.addBreakPoint(fn, line, (unicode(cond), temp, enabled, count))
            self.__resizeColumns()
            self.__resort()
    
    def __doubleClicked(self, index):
        """
        Private slot to handle the double clicked signal.
        
        @param index index of the entry that was double clicked (QModelIndex)
        """
        if index.isValid():
            self.__editBreakpoint(index)

    def __editBreak(self):
        """
        Private slot to handle the edit breakpoint context menu entry.
        """
        index = self.currentIndex()
        if index.isValid():
            self.__editBreakpoint(index)
    
    def __editBreakpoint(self, index):
        """
        Private slot to edit a breakpoint.
        
        @param index index of breakpoint to be edited (QModelIndex)
        """
        sindex = self.__toSourceIndex(index)
        if sindex.isValid():
            bp = self.__model.getBreakPointByIndex(sindex)
            if not bp:
                return
            
            fn, line, cond, temp, enabled, count = bp[:6]
            
            dlg = EditBreakpointDialog((fn, line), (cond, temp, enabled, count),
                self.condHistory, self, modal = True)
            if dlg.exec_() == QDialog.Accepted:
                cond, temp, enabled, count = dlg.getData()
                if not cond.isEmpty():
                    self.condHistory.removeAll(cond)
                    self.condHistory.prepend(cond)
                
                self.__model.setBreakPointByIndex(sindex, 
                    fn, line, (unicode(cond), temp, enabled, count))
                self.__resizeColumns()
                self.__resort()

    def __setBpEnabled(self, index, enabled):
        """
        Private method to set the enabled status of a breakpoint.
        
        @param index index of breakpoint to be enabled/disabled (QModelIndex)
        @param enabled flag indicating the enabled status to be set (boolean)
        """
        sindex = self.__toSourceIndex(index)
        if sindex.isValid():
            self.__model.setBreakPointEnabledByIndex(sindex, enabled)
        
    def __enableBreak(self):
        """
        Private slot to handle the enable breakpoint context menu entry.
        """
        index = self.currentIndex()
        self.__setBpEnabled(index, True)
        self.__resizeColumns()
        self.__resort()

    def __enableAllBreaks(self):
        """
        Private slot to handle the enable all breakpoints context menu entry.
        """
        index = self.model().index(0, 0)
        while index.isValid():
            self.__setBpEnabled(index, True)
            index = self.indexBelow(index)
        self.__resizeColumns()
        self.__resort()

    def __enableSelectedBreaks(self):
        """
        Private slot to handle the enable selected breakpoints context menu entry.
        """
        for index in self.selectedIndexes():
            if index.column() == 0:
                self.__setBpEnabled(index, True)
        self.__resizeColumns()
        self.__resort()

    def __disableBreak(self):
        """
        Private slot to handle the disable breakpoint context menu entry.
        """
        index = self.currentIndex()
        self.__setBpEnabled(index, False)
        self.__resizeColumns()
        self.__resort()

    def __disableAllBreaks(self):
        """
        Private slot to handle the disable all breakpoints context menu entry.
        """
        index = self.model().index(0, 0)
        while index.isValid():
            self.__setBpEnabled(index, False)
            index = self.indexBelow(index)
        self.__resizeColumns()
        self.__resort()

    def __disableSelectedBreaks(self):
        """
        Private slot to handle the disable selected breakpoints context menu entry.
        """
        for index in self.selectedIndexes():
            if index.column() == 0:
                self.__setBpEnabled(index, False)
        self.__resizeColumns()
        self.__resort()

    def __deleteBreak(self):
        """
        Private slot to handle the delete breakpoint context menu entry.
        """
        index = self.currentIndex()
        sindex = self.__toSourceIndex(index)
        if sindex.isValid():
            self.__model.deleteBreakPointByIndex(sindex)
        
    def __deleteAllBreaks(self):
        """
        Private slot to handle the delete all breakpoints context menu entry.
        """
        self.__model.deleteAll()

    def __deleteSelectedBreaks(self):
        """
        Private slot to handle the delete selected breakpoints context menu entry.
        """
        idxList = []
        for index in self.selectedIndexes():
            sindex = self.__toSourceIndex(index)
            if sindex.isValid() and index.column() == 0:
                idxList.append(sindex)
        self.__model.deleteBreakPoints(idxList)

    def __showSource(self):
        """
        Private slot to handle the goto context menu entry.
        """
        index = self.currentIndex()
        sindex = self.__toSourceIndex(index)
        bp = self.__model.getBreakPointByIndex(sindex)
        if not bp:
            return
        
        fn, line = bp[:2]
        self.emit(SIGNAL("sourceFile"), fn, line)
    
    def highlightBreakpoint(self, fn, lineno):
        """
        Public slot to handle the clientLine signal.
        
        @param fn filename of the breakpoint (QString)
        @param lineno line number of the breakpoint (integer)
        """
        sindex = self.__model.getBreakPointIndex(fn, lineno)
        if sindex.isValid():
            return
        
        index = self.__fromSourceIndex(sindex)
        if index.isValid():
            self.__clearSelection()
            self.__setRowSelected(index, True)
    
    def handleResetUI(self):
        """
        Public slot to reset the breakpoint viewer.
        """
        self.__clearSelection()
    
    def __showBackMenu(self):
        """
        Private slot to handle the aboutToShow signal of the background menu.
        """
        if self.model().rowCount() == 0:
            self.backMenuActions["EnableAll"].setEnabled(False)
            self.backMenuActions["DisableAll"].setEnabled(False)
            self.backMenuActions["DeleteAll"].setEnabled(False)
        else:
            self.backMenuActions["EnableAll"].setEnabled(True)
            self.backMenuActions["DisableAll"].setEnabled(True)
            self.backMenuActions["DeleteAll"].setEnabled(True)

    def __getSelectedItemsCount(self):
        """
        Private method to get the count of items selected.
        
        @return count of items selected (integer)
        """
        count = len(self.selectedIndexes()) / (self.__model.columnCount()-1)
        # column count is 1 greater than selectable
        return count
    
    def __configure(self):
        """
        Private method to open the configuration dialog.
        """
        e4App().getObject("UserInterface").showPreferences("debuggerGeneralPage")
