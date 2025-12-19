-- Generate numbers 0–11 for 30-minute slots between 7:00–18:00
WITH Hours AS (
    SELECT 7 AS H
    UNION ALL
    SELECT H + 1 FROM Hours WHERE H + 1 <= 18
),
Minutes AS (
    SELECT 0 AS M
    UNION ALL
    SELECT 30
),
Days AS (
    SELECT 0 AS DayOfWeek
    UNION ALL
    SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3
    UNION ALL SELECT 4 UNION ALL SELECT 5 UNION ALL SELECT 6
)
INSERT INTO SlotTemplates (StartTime, DayOfWeek)
SELECT 
    CAST(CONCAT(H.H, ':', FORMAT(M.M, '00'), ':00') AS TIME) AS StartTime,
    D.DayOfWeek
FROM Days D
CROSS JOIN Hours H
CROSS JOIN Minutes M
ORDER BY D.DayOfWeek, H.H, M.M
OPTION (MAXRECURSION 0);

-- Create account types
SET IDENTITY_INSERT [dbo].[AccountTypes] ON
INSERT INTO [dbo].[AccountTypes] ([Id], [Name]) VALUES (1, N'Customer')
INSERT INTO [dbo].[AccountTypes] ([Id], [Name]) VALUES (2, N'Vendor')
INSERT INTO [dbo].[AccountTypes] ([Id], [Name]) VALUES (3, N'Admin')
SET IDENTITY_INSERT [dbo].[AccountTypes] OFF

-- Create categories
SET IDENTITY_INSERT [dbo].[Categories] ON
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (1, N'Other', N'other.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (2, N'Rice Meals', N'image2.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (3, N'Noodles', N'image3.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (4, N'Western Food', N'image4.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (5, N'Fast Food', N'image5.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (6, N'Vegetarian', N'image6.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (7, N'Healthy Meals', N'image7.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (8, N'Bento & Lunch Boxes', N'image8.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (9, N'Malay Cuisine', N'image9.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (10, N'Indian Cuisine', N'image10.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (11, N'Chinese Cuisine', N'image11.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (12, N'Korean Food', N'image12.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (13, N'Japanese Food', N'image13.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (14, N'Snacks', N'image14.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (15, N'Desserts', N'image15.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (16, N'Bakery', N'image16.png')
INSERT INTO [dbo].[Categories] ([Id], [Name], [Image]) VALUES (17, N'Beverages', N'image17.png')
SET IDENTITY_INSERT [dbo].[Categories] OFF

-- Create venue
SET IDENTITY_INSERT [dbo].[Venues] ON
INSERT INTO [dbo].[Venues] ([Id], [Name]) VALUES (1, N'Other Venue')
INSERT INTO [dbo].[Venues] ([Id], [Name]) VALUES (2, N'Red Brick')
INSERT INTO [dbo].[Venues] ([Id], [Name]) VALUES (3, N'Yum Yum')
SET IDENTITY_INSERT [dbo].[Venues] OFF

-- Create system admin
SET IDENTITY_INSERT [dbo].[Accounts] ON
INSERT INTO [dbo].[Accounts] ([Id], [Name], [PhoneNumber], [Email], [PasswordHash], [Image], [FailedLoginAttempts], [CreatedAt], [DeletionAt], [LockoutEnd], [IsBanned], [IsDeleted], [AccountTypeId]) VALUES (1, N'Superchef', NULL, N'superchef.system@gmail.com', N'AQAAAAIAAYagAAAAEJzHAFqS/AXTxmUBXe4sC9lHnmo9mw0aq1tkN4AeiktH8AnRzfnInRirXweySMO8zw==', NULL, 0, N'2025-12-14 00:00:00', NULL, NULL, 0, 0, 3)
SET IDENTITY_INSERT [dbo].[Accounts] OFF
